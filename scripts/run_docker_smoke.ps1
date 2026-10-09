param(
    [string]$ImageName = 'sockseek:sprint15-smoke',

    [string]$ContainerName = '',

    [string]$DockerCli = 'docker',

    [switch]$SkipBuild,

    [switch]$SkipComposeConfig,

    [ValidateRange(1, 120)]
    [int]$HealthTimeoutSeconds = 30,

    [ValidateRange(1, 3600)]
    [int]$DockerCommandTimeoutSeconds = 300,

    [string]$DiagnosticsPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ContainerName)) {
    $safeSuffix = [Guid]::NewGuid().ToString('N').Substring(0, 12)
    $ContainerName = "sockseek-smoke-$safeSuffix"
}

$script:DiagnosticLines = New-Object System.Collections.Generic.List[string]

function Add-Diagnostic {
    param([string]$Line)

    $script:DiagnosticLines.Add($Line)
}

function Save-Diagnostics {
    if ([string]::IsNullOrWhiteSpace($DiagnosticsPath)) {
        return
    }

    $fullPath = [System.IO.Path]::GetFullPath($DiagnosticsPath)
    $directory = Split-Path -Parent $fullPath
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    Set-Content -Path $fullPath -Value $script:DiagnosticLines -Encoding UTF8
}

function Invoke-Docker {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [switch]$AllowFailure
    )

    $commandText = "$DockerCli $($Arguments -join ' ')"
    Add-Diagnostic ""
    Add-Diagnostic "## $commandText"
    Add-Diagnostic ""
    Add-Diagnostic "- Started: $((Get-Date).ToString('o'))"
    Add-Diagnostic "- Timeout: ${DockerCommandTimeoutSeconds}s"
    Add-Diagnostic ""
    Add-Diagnostic '```text'

    $job = Start-Job -ScriptBlock {
        param(
            [string]$DockerCli,
            [string[]]$Arguments
        )

        & $DockerCli @Arguments 2>&1 | ForEach-Object { $_ }
        [pscustomobject]@{ __SockseekDockerExitCode = $LASTEXITCODE }
    } -ArgumentList $DockerCli, $Arguments

    $exitCode = $null
    try {
        if (-not (Wait-Job -Job $job -Timeout $DockerCommandTimeoutSeconds)) {
            Stop-Job -Job $job
            Add-Diagnostic "TIMEOUT after ${DockerCommandTimeoutSeconds}s"
            Add-Diagnostic '```'
            Save-Diagnostics
            if ($AllowFailure) {
                Write-Host "Docker command timed out after ${DockerCommandTimeoutSeconds}s: $commandText"
                return $false
            }

            throw "Docker command timed out after ${DockerCommandTimeoutSeconds}s: $commandText"
        }

        foreach ($item in Receive-Job -Job $job) {
            if ($item.PSObject.Properties.Name -contains '__SockseekDockerExitCode') {
                $exitCode = [int]$item.__SockseekDockerExitCode
                continue
            }

            Write-Host $item
            Add-Diagnostic ([string]$item)
        }

        if ($null -eq $exitCode) {
            Add-Diagnostic '```'
            Add-Diagnostic ""
            Add-Diagnostic "- Exit code: missing"
            Save-Diagnostics
            if ($AllowFailure) {
                Write-Host "Docker command did not report an exit code: $commandText"
                return $false
            }

            throw "Docker command did not report an exit code: $commandText"
        }
    }
    finally {
        Remove-Job -Job $job -Force
    }

    Add-Diagnostic '```'
    Add-Diagnostic ""
    Add-Diagnostic "- Exit code: $exitCode"
    Save-Diagnostics

    if ($exitCode -ne 0) {
        if ($AllowFailure) {
            Write-Host "Docker command failed with exit code ${exitCode}: $commandText"
            return $false
        }

        throw "Docker command failed with exit code ${exitCode}: $commandText"
    }

    return $true
}

Add-Diagnostic "# Docker smoke diagnostics"
Add-Diagnostic ""
Add-Diagnostic "- Generated: $((Get-Date).ToString('o'))"
Add-Diagnostic "- Image: $ImageName"
Add-Diagnostic "- Container: $ContainerName"
Add-Diagnostic "- Docker CLI: $DockerCli"
Add-Diagnostic "- Working directory: $((Get-Location).Path)"
Add-Diagnostic "- Windows user: $(& whoami)"
Add-Diagnostic "- PowerShell: $($PSVersionTable.PSVersion)"
Save-Diagnostics

Write-Host "Capturing Docker context."
Invoke-Docker @('context', 'ls') -AllowFailure | Out-Null

Write-Host "Checking Docker engine availability."
Invoke-Docker @('version')

if (-not $SkipComposeConfig) {
    Write-Host "Validating docker compose configuration."
    Invoke-Docker @('compose', 'config')
}

if (-not $SkipBuild) {
    Write-Host "Building Docker image $ImageName."
    Invoke-Docker @('build', '-t', $ImageName, '.')
}

Write-Host "Checking packaged CLI help."
Invoke-Docker @('run', '--rm', '--entrypoint', '/usr/bin/sockseek', $ImageName, '--help')

Write-Host "Starting daemon container $ContainerName."
try {
    Invoke-Docker @(
        'run',
        '-d',
        '--name',
        $ContainerName,
        '--entrypoint',
        '/usr/bin/sockseek-daemon',
        $ImageName)

    $healthScript = "deadline=`$((`$(date +%s) + $HealthTimeoutSeconds)); " +
        "while [ `$(date +%s) -lt `$deadline ]; do " +
        "wget -qO- http://127.0.0.1:5030/health && exit 0; " +
        "sleep 1; " +
        "done; " +
        "echo 'Timed out waiting for daemon health endpoint.' >&2; exit 1"

    Write-Host "Waiting for daemon /health inside the container."
    Invoke-Docker @('exec', $ContainerName, 'sh', '-c', $healthScript)
}
finally {
    Write-Host "Daemon container logs:"
    Invoke-Docker @('logs', $ContainerName) -AllowFailure | Out-Null
    Write-Host "Removing daemon smoke container $ContainerName."
    Invoke-Docker @('rm', '-f', $ContainerName) -AllowFailure | Out-Null
}

Write-Host "Docker smoke completed successfully for $ImageName."
Save-Diagnostics
