param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$validator = Join-Path $PSScriptRoot 'validate_soulseek_compliance_gate.ps1'
$powerShellHost = (Get-Process -Id $PID).Path

if (-not (Test-Path -LiteralPath $validator)) {
    throw "Validator script was not found: $validator"
}

if ([string]::IsNullOrWhiteSpace($powerShellHost) -or -not (Test-Path -LiteralPath $powerShellHost)) {
    throw 'Could not resolve the current PowerShell host executable.'
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

    $process = Start-Process -FilePath $powerShellHost `
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

    $auditWithoutWishlistImplementation = [regex]::Replace(
        $audit,
        '(?im)^\| Wishlist \|.*$',
        '| Wishlist | Planned only. | Medium; a redacted live-network smoke is still required before public-beta approval. |')
    $auditWithoutWishlistImplementationPath = Write-Case 'audit-without-wishlist-implementation.md' $auditWithoutWishlistImplementation
    if ((Invoke-Validator -AuditPath $auditWithoutWishlistImplementationPath -AdrPath $adrPath -LimitationsPath $limitationsPath -ChecklistPath $checklistPath) -eq 0) {
        throw 'Expected validation to fail when the implemented wishlist coverage is removed.'
    }

    $auditWithoutWishlistLiveBlocker = [regex]::Replace(
        $audit,
        '(?im)^\| Wishlist \|(?<status>.*?)\|.*$',
        '| Wishlist | ${status}| Low; complete. |')
    $auditWithoutWishlistLiveBlockerPath = Write-Case 'audit-without-wishlist-live-blocker.md' $auditWithoutWishlistLiveBlocker
    if ((Invoke-Validator -AuditPath $auditWithoutWishlistLiveBlockerPath -AdrPath $adrPath -LimitationsPath $limitationsPath -ChecklistPath $checklistPath) -eq 0) {
        throw 'Expected validation to fail when the wishlist live-network blocker is removed without evidence.'
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

    $limitationsWithoutLiveBlocker = $limitations -replace 'Public beta remains blocked by ADR-0009 until redacted live Soulseek verification passes', 'Public beta is ready'
    $limitationsWithoutLiveBlockerPath = Write-Case 'limitations-without-live-blocker.md' $limitationsWithoutLiveBlocker
    if ((Invoke-Validator -AuditPath $auditPath -AdrPath $adrPath -LimitationsPath $limitationsWithoutLiveBlockerPath -ChecklistPath $checklistPath) -eq 0) {
        throw 'Expected validation to fail when beta limitations omit the live Soulseek verification blocker.'
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

    $checklistWithoutWindowsEvidenceValidator = $checklist -replace 'scripts/validate_windows_package_smoke_evidence\.ps1', 'Windows evidence validator omitted'
    $checklistWithoutWindowsEvidenceValidatorPath = Write-Case 'checklist-without-windows-evidence-validator.md' $checklistWithoutWindowsEvidenceValidator
    if ((Invoke-Validator -AuditPath $auditPath -AdrPath $adrPath -LimitationsPath $limitationsPath -ChecklistPath $checklistWithoutWindowsEvidenceValidatorPath) -eq 0) {
        throw 'Expected validation to fail when the checklist omits the Windows package evidence validator.'
    }

    Write-Host 'Soulseek compliance gate validator smoke passed.'
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
