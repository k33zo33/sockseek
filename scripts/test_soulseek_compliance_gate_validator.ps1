param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$validator = Join-Path $PSScriptRoot 'validate_soulseek_compliance_gate.ps1'

if (-not (Test-Path -LiteralPath $validator)) {
    throw "Validator script was not found: $validator"
}

$sourceAudit = Join-Path $repoRoot 'docs/soulseek-compliance-audit.md'
$sourceAdr = Join-Path $repoRoot 'docs/adr/0009-closed-beta-until-soulseek-compliance.md'
$sourceLimitations = Join-Path $repoRoot 'docs/beta-limitations.md'
$sourceChecklist = Join-Path $repoRoot 'docs/soulseek-public-beta-compliance-checklist.md'

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("sockseek-soulseek-gate-validator-" + [System.Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

function Invoke-Validator {
    param(
        [string]$AuditPath,
        [string]$AdrPath,
        [string]$LimitationsPath,
        [string]$ChecklistPath
    )

    $stdout = Join-Path $tempRoot ("validator-out-" + [System.Guid]::NewGuid().ToString('N') + ".txt")
    $stderr = Join-Path $tempRoot ("validator-err-" + [System.Guid]::NewGuid().ToString('N') + ".txt")
    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        "`"$validator`"",
        '-AuditPath',
        "`"$AuditPath`"",
        '-AdrPath',
        "`"$AdrPath`"",
        '-LimitationsPath',
        "`"$LimitationsPath`"",
        '-ChecklistPath',
        "`"$ChecklistPath`""
    ) -join ' '

    $process = Start-Process -FilePath 'powershell' `
        -ArgumentList $arguments `
        -Wait `
        -PassThru `
        -NoNewWindow `
        -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr

    return $process.ExitCode
}

function Write-Case {
    param(
        [string]$Name,
        [string]$Content
    )

    $path = Join-Path $tempRoot $Name
    Set-Content -LiteralPath $path -Value $Content -NoNewline
    return $path
}

try {
    $audit = Get-Content -LiteralPath $sourceAudit -Raw
    $adr = Get-Content -LiteralPath $sourceAdr -Raw
    $limitations = Get-Content -LiteralPath $sourceLimitations -Raw
    $checklist = Get-Content -LiteralPath $sourceChecklist -Raw

    $auditPath = Write-Case 'audit.md' $audit
    $adrPath = Write-Case 'adr.md' $adr
    $limitationsPath = Write-Case 'limitations.md' $limitations
    $checklistPath = Write-Case 'checklist.md' $checklist

    if ((Invoke-Validator -AuditPath $auditPath -AdrPath $adrPath -LimitationsPath $limitationsPath -ChecklistPath $checklistPath) -ne 0) {
        throw 'Expected current Soulseek compliance gate documents to pass validation.'
    }

    $auditWithoutWishlistBlocker = $audit -replace '\| Wishlist \| Not implemented in the desktop/daemon product\. \| High; explicitly listed in Soulseek rules\. \|', '| Wishlist | Implemented. | Low. |'
    $auditWithoutWishlistBlockerPath = Write-Case 'audit-without-wishlist-blocker.md' $auditWithoutWishlistBlocker
    if ((Invoke-Validator -AuditPath $auditWithoutWishlistBlockerPath -AdrPath $adrPath -LimitationsPath $limitationsPath -ChecklistPath $checklistPath) -eq 0) {
        throw 'Expected validation to fail when the wishlist blocker is removed without a superseding decision.'
    }

    $adrWithoutClosedOnlyDecision = $adr -replace 'Do not publish a public beta with Soulseek network connectivity enabled', 'Publish a public beta with Soulseek network connectivity enabled'
    $adrWithoutClosedOnlyDecisionPath = Write-Case 'adr-without-closed-only-decision.md' $adrWithoutClosedOnlyDecision
    if ((Invoke-Validator -AuditPath $auditPath -AdrPath $adrWithoutClosedOnlyDecisionPath -LimitationsPath $limitationsPath -ChecklistPath $checklistPath) -eq 0) {
        throw 'Expected validation to fail when ADR-0009 no longer blocks public beta connectivity.'
    }

    $limitationsWithoutProviderNoAudio = $limitations -replace 'Spotify, YouTube, Bandcamp and MusicBrainz are never audio sources', 'Spotify, YouTube, Bandcamp and MusicBrainz may be audio sources'
    $limitationsWithoutProviderNoAudioPath = Write-Case 'limitations-without-provider-no-audio.md' $limitationsWithoutProviderNoAudio
    if ((Invoke-Validator -AuditPath $auditPath -AdrPath $adrPath -LimitationsPath $limitationsWithoutProviderNoAudioPath -ChecklistPath $checklistPath) -eq 0) {
        throw 'Expected validation to fail when beta limitations omit the provider no-audio policy.'
    }

    $auditWithoutChecklistReference = $audit -replace 'Use `docs/soulseek-public-beta-compliance-checklist\.md` to record feature/compliance tests or manual smoke evidence for every Soulseek feature claim\.', 'Record feature/compliance tests or manual smoke evidence for every Soulseek feature claim.'
    $auditWithoutChecklistReferencePath = Write-Case 'audit-without-checklist-reference.md' $auditWithoutChecklistReference
    if ((Invoke-Validator -AuditPath $auditWithoutChecklistReferencePath -AdrPath $adrPath -LimitationsPath $limitationsPath -ChecklistPath $checklistPath) -eq 0) {
        throw 'Expected validation to fail when the compliance audit omits the public beta checklist reference.'
    }

    $checklistWithoutStopCondition = $checklist -replace 'Do not publish a public beta while ADR-0009 remains the active decision', 'Publish decisions may proceed while ADR-0009 remains active'
    $checklistWithoutStopConditionPath = Write-Case 'checklist-without-stop-condition.md' $checklistWithoutStopCondition
    if ((Invoke-Validator -AuditPath $auditPath -AdrPath $adrPath -LimitationsPath $limitationsPath -ChecklistPath $checklistWithoutStopConditionPath) -eq 0) {
        throw 'Expected validation to fail when the checklist omits the ADR-0009 public beta stop condition.'
    }

    Write-Host 'Soulseek compliance gate validator smoke passed.'
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
