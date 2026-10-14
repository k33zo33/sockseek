param(
    [string]$GoNoGoPath = 'docs/beta-go-no-go.md'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$validator = Join-Path $PSScriptRoot 'validate_beta_go_no_go.ps1'
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

    $dockerWithoutWindowsAccessDiagnostics = $baseline -replace ' the process `docker-users` membership, Docker CLI config path readability and', ''
    $dockerWithoutWindowsAccessDiagnosticsPath = Write-Case 'docker-without-windows-access-diagnostics.md' $dockerWithoutWindowsAccessDiagnostics
    if ((Invoke-Validator $dockerWithoutWindowsAccessDiagnosticsPath) -eq 0) {
        throw 'Expected validation to fail when Docker/headless smoke omits Windows process access diagnostics.'
    }

    $renderedTraceWithoutRunner = $baseline -replace ' `scripts/test_desktop_virtualization_trace_capture_runner\.ps1` smokes the three-surface runner with a fake capture helper,', ''
    $renderedTraceWithoutRunnerPath = Write-Case 'rendered-trace-without-runner.md' $renderedTraceWithoutRunner
    if ((Invoke-Validator $renderedTraceWithoutRunnerPath) -eq 0) {
        throw 'Expected validation to fail when rendered trace evidence omits the three-surface runner smoke.'
    }

    $renderedTraceWithoutOperatorActions = $baseline -replace ' and explicit operator actions', ''
    $renderedTraceWithoutOperatorActions = $renderedTraceWithoutOperatorActions -replace ', missing-operator-actions', ''
    $renderedTraceWithoutOperatorActionsPath = Write-Case 'rendered-trace-without-operator-actions.md' $renderedTraceWithoutOperatorActions
    if ((Invoke-Validator $renderedTraceWithoutOperatorActionsPath) -eq 0) {
        throw 'Expected validation to fail when rendered trace evidence omits explicit operator action checks.'
    }

    $renderedTraceWithoutFixturePrep = $baseline -replace ' `scripts/prepare_desktop_virtualization_fixture\.ps1` prepares an isolated config directory with 100k library rows, a 10k imported playlist and a 10k mock Soulseek file tree,', ''
    $renderedTraceWithoutFixturePrepPath = Write-Case 'rendered-trace-without-fixture-prep.md' $renderedTraceWithoutFixturePrep
    if ((Invoke-Validator $renderedTraceWithoutFixturePrepPath) -eq 0) {
        throw 'Expected validation to fail when rendered trace evidence omits fixture preparation.'
    }

    $renderedTraceWithoutLauncher = $baseline -replace ' `scripts/start_desktop_virtualization_fixture\.ps1` launches Desktop with that fixture environment and supports `-DesktopExecutablePath` for packaged artifact traces,', ''
    $renderedTraceWithoutLauncherPath = Write-Case 'rendered-trace-without-launcher.md' $renderedTraceWithoutLauncher
    if ((Invoke-Validator $renderedTraceWithoutLauncherPath) -eq 0) {
        throw 'Expected validation to fail when rendered trace evidence omits fixture launcher coverage.'
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

    Write-Host 'Beta go/no-go validator smoke passed.'
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
