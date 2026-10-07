param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('library', 'search', 'playlist')]
    [string]$Surface,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 1000000)]
    [int]$FixtureSize,

    [ValidateRange(10, 3600)]
    [int]$DurationSeconds = 180,

    [ValidateRange(1, 60)]
    [int]$SampleIntervalSeconds = 2,

    [string]$ProcessName = 'Sockseek.Desktop',

    [string]$OutputPath,

    [string]$SamplesCsvPath,

    [string]$Notes = 'none observed'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Format-Bytes([long]$Value) {
    if ($Value -ge 1GB) {
        return ('{0:N2} GiB' -f ($Value / 1GB))
    }

    if ($Value -ge 1MB) {
        return ('{0:N2} MiB' -f ($Value / 1MB))
    }

    if ($Value -ge 1KB) {
        return ('{0:N2} KiB' -f ($Value / 1KB))
    }

    return "$Value B"
}

function Get-ShortCommit {
    try {
        $commit = git rev-parse --short HEAD 2>$null
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($commit)) {
            return $commit.Trim()
        }
    }
    catch {
    }

    return 'unknown'
}

function Get-OperatingSystemDescription {
    try {
        $os = Get-CimInstance Win32_OperatingSystem -ErrorAction Stop
        return "$($os.Caption) $($os.Version)"
    }
    catch {
    }

    try {
        return [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
    }
    catch {
    }

    return 'unknown'
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $safeTimestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $OutputPath = Join-Path (Get-Location) "artifacts/desktop-virtualization-$Surface-$safeTimestamp.md"
}

if ([string]::IsNullOrWhiteSpace($SamplesCsvPath)) {
    $SamplesCsvPath = [System.IO.Path]::ChangeExtension($OutputPath, '.samples.csv')
}

$outputDirectory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

$samplesDirectory = Split-Path -Parent $SamplesCsvPath
if (-not [string]::IsNullOrWhiteSpace($samplesDirectory)) {
    New-Item -ItemType Directory -Force -Path $samplesDirectory | Out-Null
}

$processes = @(Get-Process -Name $ProcessName -ErrorAction SilentlyContinue)
if ($processes.Count -ne 1) {
    throw "Expected exactly one '$ProcessName' process, found $($processes.Count). Start the packaged Desktop app and close duplicate instances before capture."
}

$processId = $processes[0].Id
$startedAt = Get-Date
$deadline = $startedAt.AddSeconds($DurationSeconds)
$samples = New-Object System.Collections.Generic.List[object]

Write-Host "Capturing $Surface virtualization trace for process $ProcessName ($processId)."
Write-Host "During capture, open the $Surface surface, scroll top-to-bottom and back, and resize the window once."
Write-Host "Capture duration: $DurationSeconds seconds. Output: $OutputPath"

while ((Get-Date) -lt $deadline) {
    $process = Get-Process -Id $processId -ErrorAction Stop
    $samples.Add([pscustomobject]@{
        TimestampUtc = (Get-Date).ToUniversalTime().ToString('o')
        PrivateBytes = [long]$process.PrivateMemorySize64
        WorkingSetBytes = [long]$process.WorkingSet64
        PagedMemoryBytes = [long]$process.PagedMemorySize64
        CpuSeconds = [double]$process.CPU
    })

    Start-Sleep -Seconds $SampleIntervalSeconds
}

if ($samples.Count -lt 2) {
    throw "Expected at least two samples, captured $($samples.Count). Increase DurationSeconds or lower SampleIntervalSeconds."
}

$first = $samples[0]
$last = $samples[$samples.Count - 1]
$peakPrivate = [long](($samples | Measure-Object -Property PrivateBytes -Maximum).Maximum)
$peakWorkingSet = [long](($samples | Measure-Object -Property WorkingSetBytes -Maximum).Maximum)
$peakPaged = [long](($samples | Measure-Object -Property PagedMemoryBytes -Maximum).Maximum)
$privateDelta = [long]$last.PrivateBytes - [long]$first.PrivateBytes
$workingSetDelta = [long]$last.WorkingSetBytes - [long]$first.WorkingSetBytes
$cpuDelta = [double]$last.CpuSeconds - [double]$first.CpuSeconds
$commit = Get-ShortCommit
$osDescription = Get-OperatingSystemDescription
$displayScale = 'record manually if non-default'

$samples | Export-Csv -Path $SamplesCsvPath -NoTypeInformation -Encoding UTF8

$report = @"
# Desktop virtualization trace capture

Recorded on $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz').

## Metadata

| Field | Value |
| --- | --- |
| Commit | `$commit` |
| Surface | `$Surface` |
| Fixture size | `$FixtureSize` |
| Process | `$ProcessName` PID `$processId` |
| OS | $osDescription |
| Display scaling | $displayScale |
| Duration | $DurationSeconds seconds |
| Sample interval | $SampleIntervalSeconds seconds |
| Raw samples | `$SamplesCsvPath` |

## Operator actions

- Opened the `$Surface` surface with the stated fixture loaded.
- Scrolled from top to bottom and back.
- Resized the Desktop window between narrow and wide desktop widths.
- Checked row actions and visible text for overlap.

## Results

| Metric | Value |
| --- | --- |
| Peak private memory | $(Format-Bytes $peakPrivate) |
| Private memory delta | $(Format-Bytes $privateDelta) |
| Peak working set | $(Format-Bytes $peakWorkingSet) |
| Working set delta | $(Format-Bytes $workingSetDelta) |
| Peak paged memory | $(Format-Bytes $peakPaged) |
| CPU time delta | $('{0:N2} s' -f $cpuDelta) |
| Samples captured | $($samples.Count) |

## Observed UI defects

$Notes

## Gate note

This capture is evidence for `docs/desktop-virtualization-trace.md`. Public beta remains blocked until library, search and playlist captures are recorded for the target public-beta OS and hardware class and summarized in `docs/beta-go-no-go.md`.
"@

Set-Content -Path $OutputPath -Value $report -Encoding UTF8
Write-Host "Wrote trace report to $OutputPath"
Write-Host "Wrote raw samples to $SamplesCsvPath"
