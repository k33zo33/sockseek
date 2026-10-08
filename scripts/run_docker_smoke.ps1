param(
    [string]$ImageName = 'sockseek:sprint15-smoke',

    [string]$ContainerName = '',

    [string]$DockerCli = 'docker',

    [switch]$SkipBuild,

    [switch]$SkipComposeConfig,

    [ValidateRange(1, 120)]
    [int]$HealthTimeoutSeconds = 30
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

    & $DockerCli @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Docker command failed with exit code ${LASTEXITCODE}: $DockerCli $($Arguments -join ' ')"
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
    Write-Host "Removing daemon smoke container $ContainerName."
    & $DockerCli rm -f $ContainerName | Out-Null
}

Write-Host "Docker smoke completed successfully for $ImageName."
