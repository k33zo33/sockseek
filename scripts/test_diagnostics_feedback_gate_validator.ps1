param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$validator = Join-Path $PSScriptRoot 'validate_diagnostics_feedback_gate.ps1'

if (-not (Test-Path -LiteralPath $validator)) {
    throw "Validator script was not found: $validator"
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("sockseek-diagnostics-gate-validator-" + [System.Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

function Copy-Source {
    param(
        [string]$SourceRelativePath,
        [string]$Name
    )

    $source = Join-Path $repoRoot $SourceRelativePath
    $target = Join-Path $tempRoot $Name
    Copy-Item -LiteralPath $source -Destination $target
    return $target
}

function Invoke-Validator {
    param(
        [string]$DiagnosticsPath,
        [string]$BugTemplatePath,
        [string]$CrashTemplatePath,
        [string]$IssueConfigPath,
        [string]$SecurityPath
    )

    $stdout = Join-Path $tempRoot ("validator-out-" + [System.Guid]::NewGuid().ToString('N') + ".txt")
    $stderr = Join-Path $tempRoot ("validator-err-" + [System.Guid]::NewGuid().ToString('N') + ".txt")
    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        "`"$validator`"",
        '-DiagnosticsPath',
        "`"$DiagnosticsPath`"",
        '-BugTemplatePath',
        "`"$BugTemplatePath`"",
        '-CrashTemplatePath',
        "`"$CrashTemplatePath`"",
        '-IssueConfigPath',
        "`"$IssueConfigPath`"",
        '-SecurityPath',
        "`"$SecurityPath`""
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
    $diagnosticsPath = Copy-Source 'docs/diagnostics-feedback.md' 'diagnostics-feedback.md'
    $bugTemplatePath = Copy-Source '.github/ISSUE_TEMPLATE/bug_report.md' 'bug_report.md'
    $crashTemplatePath = Copy-Source '.github/ISSUE_TEMPLATE/crash_report.md' 'crash_report.md'
    $issueConfigPath = Copy-Source '.github/ISSUE_TEMPLATE/config.yml' 'config.yml'
    $securityPath = Copy-Source 'SECURITY.md' 'SECURITY.md'

    if ((Invoke-Validator -DiagnosticsPath $diagnosticsPath -BugTemplatePath $bugTemplatePath -CrashTemplatePath $crashTemplatePath -IssueConfigPath $issueConfigPath -SecurityPath $securityPath) -ne 0) {
        throw 'Expected current diagnostics feedback gate documents to pass validation.'
    }

    $bugTemplate = Get-Content -LiteralPath $bugTemplatePath -Raw
    $bugWithoutAuthWarning = $bugTemplate -replace 'full Authorization headers, ', ''
    $bugWithoutAuthWarningPath = Write-Case 'bug-without-auth-warning.md' $bugWithoutAuthWarning
    if ((Invoke-Validator -DiagnosticsPath $diagnosticsPath -BugTemplatePath $bugWithoutAuthWarningPath -CrashTemplatePath $crashTemplatePath -IssueConfigPath $issueConfigPath -SecurityPath $securityPath) -eq 0) {
        throw 'Expected validation to fail when the bug template omits Authorization header redaction.'
    }

    $config = Get-Content -LiteralPath $issueConfigPath -Raw
    $configWithBlankIssues = $config -replace 'blank_issues_enabled: false', 'blank_issues_enabled: true'
    $configWithBlankIssuesPath = Write-Case 'config-with-blank-issues.yml' $configWithBlankIssues
    if ((Invoke-Validator -DiagnosticsPath $diagnosticsPath -BugTemplatePath $bugTemplatePath -CrashTemplatePath $crashTemplatePath -IssueConfigPath $configWithBlankIssuesPath -SecurityPath $securityPath) -eq 0) {
        throw 'Expected validation to fail when blank public issues are enabled.'
    }

    $diagnostics = Get-Content -LiteralPath $diagnosticsPath -Raw
    $diagnosticsWithoutPublicBetaBlock = $diagnostics -replace 'Public beta remains blocked', 'Public beta is ready'
    $diagnosticsWithoutPublicBetaBlockPath = Write-Case 'diagnostics-without-public-beta-block.md' $diagnosticsWithoutPublicBetaBlock
    if ((Invoke-Validator -DiagnosticsPath $diagnosticsWithoutPublicBetaBlockPath -BugTemplatePath $bugTemplatePath -CrashTemplatePath $crashTemplatePath -IssueConfigPath $issueConfigPath -SecurityPath $securityPath) -eq 0) {
        throw 'Expected validation to fail when diagnostics docs omit the public beta blocker.'
    }

    Write-Host 'Diagnostics feedback gate validator smoke passed.'
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
