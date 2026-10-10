param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$smokeHelper = Join-Path $PSScriptRoot 'run_docker_smoke.ps1'
if (-not (Test-Path -LiteralPath $smokeHelper)) {
    throw "Docker smoke helper was not found: $smokeHelper"
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("sockseek-docker-smoke-diagnostics-" + [System.Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

try {
    $fakeBin = Join-Path $tempRoot 'bin'
    New-Item -ItemType Directory -Path $fakeBin | Out-Null
    $fakeDocker = Join-Path $fakeBin 'docker.cmd'

    @'
@echo off
echo FAKE_DOCKER_ARGS:%*
if "%1"=="context" (
  echo NAME              DESCRIPTION    DOCKER ENDPOINT
  echo desktop-linux *   Docker Desktop npipe:////./pipe/dockerDesktopLinuxEngine
  exit /b 0
)
if "%1"=="-H" (
  echo FAKE_DIRECT_PIPE_CHECK
  echo error during connect: open //./pipe/dockerDesktopLinuxEngine: Access is denied. 1>&2
  exit /b 1
)
if "%1"=="version" (
  echo FAKE_MAIN_VERSION_CHECK
  echo error during connect: open //./pipe/docker_engine: Access is denied. 1>&2
  exit /b 1
)
echo unexpected fake docker invocation: %* 1>&2
exit /b 99
'@ | Set-Content -LiteralPath $fakeDocker -Encoding ASCII

    $diagnosticsPath = Join-Path $tempRoot 'docker-smoke-diagnostics.md'
    $stdoutPath = Join-Path $tempRoot 'docker-smoke.stdout.txt'
    $stderrPath = Join-Path $tempRoot 'docker-smoke.stderr.txt'
    $previousPath = $env:PATH
    $env:PATH = "$fakeBin;$previousPath"
    try {
        $arguments = @(
            '-NoProfile',
            '-ExecutionPolicy',
            'Bypass',
            '-File',
            "`"$smokeHelper`"",
            '-SkipBuild',
            '-SkipComposeConfig',
            '-DockerCommandTimeoutSeconds',
            '3',
            '-DockerDesktopPipeDiagnosticTimeoutSeconds',
            '3',
            '-DiagnosticsPath',
            "`"$diagnosticsPath`""
        ) -join ' '

        $process = Start-Process -FilePath 'powershell' `
            -ArgumentList $arguments `
            -Wait `
            -PassThru `
            -NoNewWindow `
            -RedirectStandardOutput $stdoutPath `
            -RedirectStandardError $stderrPath

        if ($process.ExitCode -eq 0) {
            throw 'Expected Docker smoke helper to fail when fake docker version fails.'
        }
    }
    finally {
        $env:PATH = $previousPath
    }

    if (-not (Test-Path -LiteralPath $diagnosticsPath)) {
        throw "Docker smoke helper did not write diagnostics: $diagnosticsPath"
    }

    $diagnostics = Get-Content -LiteralPath $diagnosticsPath -Raw
    $requiredEvidence = @(
        '# Docker smoke diagnostics',
        'FAKE_DOCKER_ARGS:context ls',
        '## Windows Docker Desktop pipe diagnostic',
        'FAKE_DOCKER_ARGS:-H npipe:////./pipe/dockerDesktopLinuxEngine version',
        'DOCKER_CONFIG override:',
        'FAKE_DIRECT_PIPE_CHECK',
        'FAKE_MAIN_VERSION_CHECK',
        'Exit code: 1'
    )

    foreach ($evidence in $requiredEvidence) {
        if ($diagnostics.IndexOf($evidence, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw "Docker diagnostics smoke is missing expected evidence: $evidence"
        }
    }

    if ($diagnostics -match 'unexpected fake docker invocation') {
        throw 'Docker smoke helper invoked an unexpected fake docker command during diagnostics-only failure path.'
    }

    Write-Host 'Docker smoke diagnostics test passed.'
}
finally {
    Set-Location $repoRoot
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
