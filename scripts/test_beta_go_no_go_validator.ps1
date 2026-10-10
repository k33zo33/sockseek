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

    $dockerPassed = $baseline -replace '\|\s*Docker/headless smoke\s*\|(.*?)\|\s*Environment-blocked\s*\|', '| Docker/headless smoke |$1| Passed |'
    $dockerPassedPath = Write-Case 'docker-passed.md' $dockerPassed
    if ((Invoke-Validator $dockerPassedPath) -eq 0) {
        throw 'Expected validation to fail when Docker/headless smoke is marked passed while public beta remains blocked.'
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
