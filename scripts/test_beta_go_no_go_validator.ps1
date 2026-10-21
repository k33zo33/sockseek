param(
    [string]$GoNoGoPath = 'docs/beta-go-no-go.md'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$validator = Join-Path $PSScriptRoot 'validate_beta_go_no_go.ps1'
$powerShellHost = (Get-Process -Id $PID).Path
$sourcePath = if ([System.IO.Path]::IsPathRooted($GoNoGoPath)) {
    $GoNoGoPath
} else {
    Join-Path $repoRoot $GoNoGoPath
}

if (-not (Test-Path -LiteralPath $validator)) {
    throw "Validator script was not found: $validator"
}

if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw "Beta go/no-go document was not found: $sourcePath"
}

if ([string]::IsNullOrWhiteSpace($powerShellHost) -or -not (Test-Path -LiteralPath $powerShellHost)) {
    throw 'Could not resolve the current PowerShell host executable.'
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("sockseek-beta-go-no-go-validator-" + [System.Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

function Invoke-Validator {
    param(
        [string]$Path
    )

    $stdout = Join-Path $tempRoot ("validator-out-" + [System.Guid]::NewGuid().ToString('N') + ".txt")
    $stderr = Join-Path $tempRoot ("validator-err-" + [System.Guid]::NewGuid().ToString('N') + ".txt")
    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        "`"$validator`"",
        '-GoNoGoPath',
        "`"$Path`""
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
    $baseline = Get-Content -LiteralPath $sourcePath -Raw

    $baselinePath = Write-Case 'baseline.md' $baseline
    if ((Invoke-Validator $baselinePath) -ne 0) {
        throw 'Expected the current beta go/no-go document to pass validation.'
    }

    $publicGo = $baseline -replace '\|\s*Public beta\s*\|\s*NO-GO\s*\|', '| Public beta | GO |'
    $publicGoPath = Write-Case 'public-go.md' $publicGo
    if ((Invoke-Validator $publicGoPath) -eq 0) {
        throw 'Expected validation to fail when public beta is marked GO.'
    }

    $withoutLiveBlocker = $baseline -replace 'dedicated-account live Soulseek checklist', 'remaining network checks'
    $withoutLiveBlockerPath = Write-Case 'without-live-blocker.md' $withoutLiveBlocker
    if ((Invoke-Validator $withoutLiveBlockerPath) -eq 0) {
        throw 'Expected validation to fail when the public-beta decision omits the live Soulseek blocker.'
    }

    $withoutWindowsElevenBlocker = $baseline -replace 'exact Windows 11 x64 package smoke', 'remaining package checks'
    $withoutWindowsElevenBlockerPath = Write-Case 'without-windows-11-blocker.md' $withoutWindowsElevenBlocker
    if ((Invoke-Validator $withoutWindowsElevenBlockerPath) -eq 0) {
        throw 'Expected validation to fail when the public-beta decision omits the Windows 11 blocker.'
    }

    $dockerRegressed = $baseline -replace '\|\s*Docker/headless smoke\s*\|(.*?)\|\s*Passed\s*\|', '| Docker/headless smoke |$1| Environment-blocked |'
    $dockerRegressedPath = Write-Case 'docker-regressed.md' $dockerRegressed
    if ((Invoke-Validator $dockerRegressedPath) -eq 0) {
        throw 'Expected validation to fail when Docker/headless smoke regresses to environment-blocked.'
    }

    $dockerWithoutHealthEvidence = $baseline -replace 'daemon container `/health` returning `\{"status":"ok"\}`', 'daemon health result omitted'
    $dockerWithoutHealthEvidencePath = Write-Case 'docker-without-health-evidence.md' $dockerWithoutHealthEvidence
    if ((Invoke-Validator $dockerWithoutHealthEvidencePath) -eq 0) {
        throw 'Expected validation to fail when Docker/headless smoke omits daemon health evidence.'
    }

    $dockerWithoutTrimEvidence = $baseline -replace ' with trimming disabled until the dependency graph is reviewed as trim-safe', ''
    $dockerWithoutTrimEvidence = $dockerWithoutTrimEvidence -replace ' without trim-analysis warning output', ''
    $dockerWithoutTrimEvidencePath = Write-Case 'docker-without-trim-evidence.md' $dockerWithoutTrimEvidence
    if ((Invoke-Validator $dockerWithoutTrimEvidencePath) -eq 0) {
        throw 'Expected validation to fail when Docker/headless smoke omits trimming-disabled evidence.'
    }

    $dockerWithoutWindowsAccessDiagnostics = $baseline -replace 'process `docker-users` membership, ', ''
    $dockerWithoutWindowsAccessDiagnostics = $dockerWithoutWindowsAccessDiagnostics -replace 'Docker CLI config path readability, ', ''
    $dockerWithoutWindowsAccessDiagnosticsPath = Write-Case 'docker-without-windows-access-diagnostics.md' $dockerWithoutWindowsAccessDiagnostics
    if ((Invoke-Validator $dockerWithoutWindowsAccessDiagnosticsPath) -eq 0) {
        throw 'Expected validation to fail when Docker/headless smoke omits Windows process access diagnostics.'
    }

    $dockerWithoutWindowsHostStateDiagnostics = $baseline -replace ', Docker Desktop service state, WSL distribution state', ''
    $dockerWithoutWindowsHostStateDiagnosticsPath = Write-Case 'docker-without-windows-host-state-diagnostics.md' $dockerWithoutWindowsHostStateDiagnostics
    if ((Invoke-Validator $dockerWithoutWindowsHostStateDiagnosticsPath) -eq 0) {
        throw 'Expected validation to fail when Docker/headless smoke omits Windows service and WSL diagnostics.'
    }

    $renderedTraceRegressed = $baseline -replace '\|\s*Rendered UI virtualization trace\s*\|(.*?)\|\s*Passed for Windows RC\s*\|', '| Rendered UI virtualization trace |$1| Incomplete |'
    $renderedTraceRegressedPath = Write-Case 'rendered-trace-regressed.md' $renderedTraceRegressed
    if ((Invoke-Validator $renderedTraceRegressedPath) -eq 0) {
        throw 'Expected validation to fail when the completed rendered trace regresses to incomplete.'
    }

    $renderedTraceWithoutValidator = $baseline -replace 'scripts/validate_desktop_virtualization_traces\.ps1', 'rendered trace validator omitted'
    $renderedTraceWithoutValidatorPath = Write-Case 'rendered-trace-without-validator.md' $renderedTraceWithoutValidator
    if ((Invoke-Validator $renderedTraceWithoutValidatorPath) -eq 0) {
        throw 'Expected validation to fail when rendered trace evidence omits the validator pass.'
    }

    $renderedTraceWithoutRuntimeEvidence = $baseline -replace '180 seconds with 90 raw samples', 'duration and samples omitted'
    $renderedTraceWithoutRuntimeEvidencePath = Write-Case 'rendered-trace-without-runtime-evidence.md' $renderedTraceWithoutRuntimeEvidence
    if ((Invoke-Validator $renderedTraceWithoutRuntimeEvidencePath) -eq 0) {
        throw 'Expected validation to fail when rendered trace evidence omits duration and sample count.'
    }

    $renderedTraceWithoutPreflight = $baseline -replace 'Exact-package fixture preflight independently confirmed 100,000 SQLite library rows, 10,000 SQLite playlist items and 10,000 mock Soulseek files\.', 'Fixture counts omitted.'
    $renderedTraceWithoutPreflightPath = Write-Case 'rendered-trace-without-preflight.md' $renderedTraceWithoutPreflight
    if ((Invoke-Validator $renderedTraceWithoutPreflightPath) -eq 0) {
        throw 'Expected validation to fail when rendered trace evidence omits exact-package fixture counts.'
    }

    $renderedTraceWithoutFixtureEvidence = $baseline -replace '`100000 tracks`', 'library fixture omitted'
    $renderedTraceWithoutFixtureEvidence = $renderedTraceWithoutFixtureEvidence -replace '`Revision 10000`', 'search fixture omitted'
    $renderedTraceWithoutFixtureEvidence = $renderedTraceWithoutFixtureEvidence -replace '`10000/10000 tracks`', 'playlist fixture omitted'
    $renderedTraceWithoutFixtureEvidencePath = Write-Case 'rendered-trace-without-fixture-evidence.md' $renderedTraceWithoutFixtureEvidence
    if ((Invoke-Validator $renderedTraceWithoutFixtureEvidencePath) -eq 0) {
        throw 'Expected validation to fail when rendered trace evidence omits the three large fixture results.'
    }

    $renderedTraceWithoutVisualEvidence = $baseline -replace 'No text overlap, unreachable row actions or unbounded growth was observed\.', 'Visual observations omitted.'
    $renderedTraceWithoutVisualEvidencePath = Write-Case 'rendered-trace-without-visual-evidence.md' $renderedTraceWithoutVisualEvidence
    if ((Invoke-Validator $renderedTraceWithoutVisualEvidencePath) -eq 0) {
        throw 'Expected validation to fail when rendered trace evidence omits visual defect observations.'
    }

    $soulseekWithoutGateValidator = $baseline -replace ' `scripts/validate_soulseek_compliance_gate\.ps1` verifies that ADR-0009 remains accepted, public beta with Soulseek network connectivity remains blocked, wishlist/upload-sharing/chat/privilege gaps remain represented as high-risk blockers, the public beta checklist remains linked from the audit, and beta limitations retain the provider no-audio policy; `scripts/test_soulseek_compliance_gate_validator\.ps1` covers negative cases for accidentally removing those blockers or checklist evidence\.', ''
    $soulseekWithoutGateValidatorPath = Write-Case 'soulseek-without-gate-validator.md' $soulseekWithoutGateValidator
    if ((Invoke-Validator $soulseekWithoutGateValidatorPath) -eq 0) {
        throw 'Expected validation to fail when Soulseek compliance decision omits the dedicated gate validator evidence.'
    }

    $soulseekWithoutChecklistEvidence = $baseline -replace ' `docs/soulseek-public-beta-compliance-checklist\.md` defines the required pre-public-beta evidence for search, wishlist, download, upload/sharing, chat, privileges, folder browse, sharing status, legal-use messaging and the provider audio invariant, while preserving ADR-0009 as the active blocker\.', ''
    $soulseekWithoutChecklistEvidencePath = Write-Case 'soulseek-without-checklist-evidence.md' $soulseekWithoutChecklistEvidence
    if ((Invoke-Validator $soulseekWithoutChecklistEvidencePath) -eq 0) {
        throw 'Expected validation to fail when Soulseek compliance decision omits public beta checklist evidence.'
    }

    $securityWithoutDiagnosticsValidator = $baseline -replace ' `scripts/validate_diagnostics_feedback_gate\.ps1` verifies private vulnerability routing, closed-beta/public-beta-blocker language, exact build evidence fields, `Copy diagnostics` guidance and public issue redaction warnings for tokens, OAuth material, Soulseek passwords, Authorization headers and private playlist URLs; `scripts/test_diagnostics_feedback_gate_validator\.ps1` covers negative cases for missing redaction warnings, blank public issues and missing public-beta blocker language\.', ''
    $securityWithoutDiagnosticsValidatorPath = Write-Case 'security-without-diagnostics-validator.md' $securityWithoutDiagnosticsValidator
    if ((Invoke-Validator $securityWithoutDiagnosticsValidatorPath) -eq 0) {
        throw 'Expected validation to fail when security/crash reporting omits diagnostics feedback validator evidence.'
    }

    $packageWithoutMetadata = $baseline -replace 'release-metadata\.json', 'release-metadata-omitted.json'
    $packageWithoutMetadataPath = Write-Case 'package-without-release-metadata.md' $packageWithoutMetadata
    if ((Invoke-Validator $packageWithoutMetadataPath) -eq 0) {
        throw 'Expected validation to fail when Windows package smoke omits release metadata evidence.'
    }

    $packageWithoutTesterInstructions = $baseline -replace 'closed-beta-tester-instructions\.md', 'closed-beta-instructions-omitted.md'
    $packageWithoutTesterInstructionsPath = Write-Case 'package-without-tester-instructions.md' $packageWithoutTesterInstructions
    if ((Invoke-Validator $packageWithoutTesterInstructionsPath) -eq 0) {
        throw 'Expected validation to fail when Windows package smoke omits closed beta tester instructions evidence.'
    }

    $packageWithoutArchiveHash = $baseline -replace '2d41a5f4cedd496ff8ea3e7adac13347634eb922aa7e6f1c07289ab64c210dde', 'archive hash omitted'
    $packageWithoutArchiveHashPath = Write-Case 'package-without-archive-hash.md' $packageWithoutArchiveHash
    if ((Invoke-Validator $packageWithoutArchiveHashPath) -eq 0) {
        throw 'Expected validation to fail when Windows package smoke omits the exact archive hash.'
    }

    $ubuntuWithoutArchiveHash = $baseline -replace 'ee7072fba179799c2911b49bb75860b6d82406426a53945e6c1a8198a25670fd', 'archive hash omitted'
    $ubuntuWithoutArchiveHashPath = Write-Case 'ubuntu-without-archive-hash.md' $ubuntuWithoutArchiveHash
    if ((Invoke-Validator $ubuntuWithoutArchiveHashPath) -eq 0) {
        throw 'Expected validation to fail when Ubuntu package smoke omits the exact archive hash.'
    }

    $windowsElevenSubstituted = $baseline -replace 'does not satisfy the required Windows 11 x64 gate', 'is accepted as Windows 11 evidence'
    $windowsElevenSubstitutedPath = Write-Case 'windows-11-substituted.md' $windowsElevenSubstituted
    if ((Invoke-Validator $windowsElevenSubstitutedPath) -eq 0) {
        throw 'Expected validation to fail when Windows 10 is substituted for Windows 11 evidence.'
    }

    Write-Host 'Beta go/no-go validator smoke passed.'
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
