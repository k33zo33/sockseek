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

    [string]$Configuration = 'Release',

    [bool]$NoBuild = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ReportPath = [System.IO.Path]::GetFullPath($ReportPath)

$environmentValues = @{
    SOCKSEEK_RUN_SOAK = '1'
    SOCKSEEK_SOAK_MINUTES = $Minutes.ToString()
    SOCKSEEK_SOAK_ITEMS_PER_CYCLE = $ItemsPerCycle.ToString()
    SOCKSEEK_SOAK_CYCLE_DELAY_MS = $CycleDelayMs.ToString()
    SOCKSEEK_SOAK_MAX_MANAGED_GROWTH_MIB = $MaxManagedGrowthMiB.ToString()
    SOCKSEEK_SOAK_MAX_PRIVATE_GROWTH_MIB = $MaxPrivateGrowthMiB.ToString()
    SOCKSEEK_SOAK_REPORT_PATH = $ReportPath
}

$previousValues = @{}
foreach ($key in $environmentValues.Keys) {
    $previousValues[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
}

$reportDirectory = Split-Path -Parent $ReportPath
if (-not [string]::IsNullOrWhiteSpace($reportDirectory)) {
    New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
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

Write-Host "Running Sprint 15 soak gate for $Minutes minute(s)."
Write-Host "Items per cycle: $ItemsPerCycle; cycle delay: $CycleDelayMs ms."
Write-Host "Managed budget: $MaxManagedGrowthMiB MiB; private budget: $MaxPrivateGrowthMiB MiB."
Write-Host "Report path: $ReportPath"

try {
    foreach ($entry in $environmentValues.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }

    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    foreach ($entry in $previousValues.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
}
