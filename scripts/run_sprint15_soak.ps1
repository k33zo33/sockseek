param(
    [ValidateRange(1, 600)]
    [int]$Minutes = 480,

    [ValidateRange(1, 100000)]
    [int]$ItemsPerCycle = 100,

    [ValidateRange(0, 3600000)]
    [int]$CycleDelayMs = 30000,

    [ValidateRange(1, 1048576)]
    [int]$MaxManagedGrowthMiB = 256,

    [ValidateRange(1, 1048576)]
    [int]$MaxPrivateGrowthMiB = 512,

    [string]$ReportPath = 'artifacts/sprint-15-soak-report.json',

    [string]$LogPath = '',

    [string]$Configuration = 'Release',

    [bool]$NoBuild = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Format-MiB([long]$Bytes) {
    return ('{0:N2} MiB' -f ($Bytes / 1MB))
}

$ReportPath = [System.IO.Path]::GetFullPath($ReportPath)
if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = [System.IO.Path]::ChangeExtension($ReportPath, '.log')
}
else {
    $LogPath = [System.IO.Path]::GetFullPath($LogPath)
}

$commit = (& git rev-parse --short HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($commit)) {
    $commit = 'unknown'
}

$environmentValues = @{
    SOCKSEEK_RUN_SOAK = '1'
    SOCKSEEK_SOAK_MINUTES = $Minutes.ToString()
    SOCKSEEK_SOAK_ITEMS_PER_CYCLE = $ItemsPerCycle.ToString()
    SOCKSEEK_SOAK_CYCLE_DELAY_MS = $CycleDelayMs.ToString()
    SOCKSEEK_SOAK_MAX_MANAGED_GROWTH_MIB = $MaxManagedGrowthMiB.ToString()
    SOCKSEEK_SOAK_MAX_PRIVATE_GROWTH_MIB = $MaxPrivateGrowthMiB.ToString()
    SOCKSEEK_SOAK_REPORT_PATH = $ReportPath
    SOCKSEEK_SOAK_COMMIT = $commit
}

$previousValues = @{}
foreach ($key in $environmentValues.Keys) {
    $previousValues[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
}

$reportDirectory = Split-Path -Parent $ReportPath
if (-not [string]::IsNullOrWhiteSpace($reportDirectory)) {
    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
}

$logDirectory = Split-Path -Parent $LogPath
if (-not [string]::IsNullOrWhiteSpace($logDirectory)) {
    New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
}

$arguments = @(
    'test',
    'Sockseek.Server.Tests\Sockseek.Server.Tests.csproj',
    '-c',
    $Configuration,
    '--filter',
    'SoakStabilityTests',
    '--logger',
    'console;verbosity=detailed'
)

if ($NoBuild) {
    $arguments += '--no-build'
}

$transcriptStarted = $false
Start-Transcript -Path $LogPath -Force | Out-Null
$transcriptStarted = $true

Write-Host "Running Sprint 15 soak gate for $Minutes minute(s)."
Write-Host "Commit: $commit"
Write-Host "Items per cycle: $ItemsPerCycle; cycle delay: $CycleDelayMs ms."
Write-Host "Managed budget: $MaxManagedGrowthMiB MiB; private budget: $MaxPrivateGrowthMiB MiB."
Write-Host "Report path: $ReportPath"
Write-Host "Log path: $LogPath"

try {
    foreach ($entry in $environmentValues.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }

    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    if (Test-Path -LiteralPath $ReportPath) {
        $report = Get-Content -LiteralPath $ReportPath -Raw | ConvertFrom-Json
        Write-Host "Soak report summary:"
        Write-Host "  Commit: $($report.Commit)"
        Write-Host "  Actual duration: $($report.ActualDuration)"
        Write-Host "  Cycles: $($report.CycleCount); retained workflows: $($report.RetainedWorkflowCount)"
        Write-Host "  Managed growth: $(Format-MiB ([long]$report.ManagedHeapGrowthBytes)) / budget $(Format-MiB ([long]$report.MaxManagedHeapGrowthBytes))"
        Write-Host "  Private growth: $(Format-MiB ([long]$report.PrivateGrowthBytes)) / budget $(Format-MiB ([long]$report.MaxPrivateGrowthBytes))"
    }
    else {
        throw "Soak test completed but report file was not written: $ReportPath"
    }
}
finally {
    foreach ($entry in $previousValues.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }

    if ($transcriptStarted) {
        Stop-Transcript | Out-Null
    }
}
