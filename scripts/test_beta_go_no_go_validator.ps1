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

    $soulseekWithoutGateValidator = $baseline -replace ' `scripts/validate_soulseek_compliance_gate\.ps1` verifies that ADR-0009 remains accepted, public beta with Soulseek network connectivity remains blocked, wishlist/upload-sharing/chat/privilege gaps remain represented as high-risk blockers, and beta limitations retain the provider no-audio policy; `scripts/test_soulseek_compliance_gate_validator\.ps1` covers negative cases for accidentally removing those blockers\.', ''
    $soulseekWithoutGateValidatorPath = Write-Case 'soulseek-without-gate-validator.md' $soulseekWithoutGateValidator
    if ((Invoke-Validator $soulseekWithoutGateValidatorPath) -eq 0) {
        throw 'Expected validation to fail when Soulseek compliance decision omits the dedicated gate validator evidence.'
    }

    $securityWithoutDiagnosticsValidator = $baseline -replace ' `scripts/validate_diagnostics_feedback_gate\.ps1` verifies private vulnerability routing, closed-beta/public-beta-blocker language, exact build evidence fields, `Copy diagnostics` guidance and public issue redaction warnings for tokens, OAuth material, Soulseek passwords, Authorization headers and private playlist URLs; `scripts/test_diagnostics_feedback_gate_validator\.ps1` covers negative cases for missing redaction warnings, blank public issues and missing public-beta blocker language\.', ''
    $securityWithoutDiagnosticsValidatorPath = Write-Case 'security-without-diagnostics-validator.md' $securityWithoutDiagnosticsValidator
    if ((Invoke-Validator $securityWithoutDiagnosticsValidatorPath) -eq 0) {
        throw 'Expected validation to fail when security/crash reporting omits diagnostics feedback validator evidence.'
    }

    $packageWithoutWarning = $baseline -replace ' The publish steps emitted `NU1900` package-vulnerability-source warnings because `https://api\.nuget\.org/v3/index\.json` was unavailable during this run; the separate dependency vulnerability scan gate remains recorded above\.', ''
    $packageWithoutWarningPath = Write-Case 'package-without-warning.md' $packageWithoutWarning
    if ((Invoke-Validator $packageWithoutWarningPath) -eq 0) {
        throw 'Expected validation to fail when Windows package smoke omits the documented NU1900 warning evidence.'
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
