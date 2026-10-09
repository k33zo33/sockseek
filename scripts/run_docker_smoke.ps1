param(
    [string]$ImageName = 'sockseek:sprint15-smoke',

    [string]$ContainerName = '',

    [string]$DockerCli = 'docker',

    [switch]$SkipBuild,

    [switch]$SkipComposeConfig,

    [ValidateRange(1, 120)]
    [int]$HealthTimeoutSeconds = 30,

    [ValidateRange(1, 3600)]
    [int]$DockerCommandTimeoutSeconds = 300
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ContainerName)) {
    $safeSuffix = [Guid]::NewGuid().ToString('N').Substring(0, 12)
    $ContainerName = "sockseek-smoke-$safeSuffix"
}

function Invoke-Docker {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

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
            throw "Docker command timed out after ${DockerCommandTimeoutSeconds}s: $DockerCli $($Arguments -join ' ')"
        }

        foreach ($item in Receive-Job -Job $job) {
            if ($item.PSObject.Properties.Name -contains '__SockseekDockerExitCode') {
                $exitCode = [int]$item.__SockseekDockerExitCode
                continue
            }

            Write-Host $item
        }

        if ($null -eq $exitCode) {
            throw "Docker command did not report an exit code: $DockerCli $($Arguments -join ' ')"
        }
    }
    finally {
        Remove-Job -Job $job -Force
    }

    if ($exitCode -ne 0) {
        throw "Docker command failed with exit code ${exitCode}: $DockerCli $($Arguments -join ' ')"
    }
}

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
    & $DockerCli logs $ContainerName 2>&1 | ForEach-Object { Write-Host $_ }
    Write-Host "Removing daemon smoke container $ContainerName."
    & $DockerCli rm -f $ContainerName | Out-Null
}

Write-Host "Docker smoke completed successfully for $ImageName."
