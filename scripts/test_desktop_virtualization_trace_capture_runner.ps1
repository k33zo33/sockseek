param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$runner = Join-Path $PSScriptRoot 'run_desktop_virtualization_trace_capture.ps1'
if (-not (Test-Path -LiteralPath $runner)) {
    throw "Trace capture runner was not found: $runner"
}

$validator = Join-Path $PSScriptRoot 'validate_desktop_virtualization_traces.ps1'
if (-not (Test-Path -LiteralPath $validator)) {
    throw "Trace validator was not found: $validator"
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("sockseek-desktop-trace-runner-" + [System.Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

try {
    $fakeCapture = Join-Path $tempRoot 'fake-capture.ps1'
    @'
param(
    [string]$Surface,
    [int]$FixtureSize,
    [int]$DurationSeconds,
    [int]$SampleIntervalSeconds,
    [string]$ProcessName,
    [string]$OutputPath,
    [string]$SamplesCsvPath,
    [string]$DisplayScaling,
    [string]$Notes
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

@"
"TimestampUtc","PrivateBytes","WorkingSetBytes","PagedMemoryBytes","CpuSeconds"
"2026-10-11T10:00:00.0000000Z","100000000","120000000","1000000","1"
"2026-10-11T10:00:02.0000000Z","101000000","121000000","1000000","2"
"@ | Set-Content -LiteralPath $SamplesCsvPath -NoNewline

@"
# Desktop virtualization trace capture

Recorded on 2026-10-11 10:00:00 +02:00.

## Metadata

| Field | Value |
| --- | --- |
| Commit | ``abc1234`` |
| Surface | ``$Surface`` |
| Fixture size | ``$FixtureSize`` |
| Process | ``$ProcessName`` PID ``1234`` |
| Process start time | ``2026-10-11T09:59:00.0000000+02:00`` |
| Main window title | ``Sockseek`` |
| Main window present | ``True`` |
| Headless process allowed | ``False`` |
| OS | Windows test fixture |
| Display scaling | $DisplayScaling |
| Duration | $DurationSeconds seconds |
| Sample interval | $SampleIntervalSeconds seconds |
| Raw samples | ``$SamplesCsvPath`` |

## Operator actions

- Opened the $Surface surface with the stated fixture loaded.
- Scrolled from top to bottom and back.
- Resized the Desktop window between narrow and wide desktop widths.
- Checked row actions and visible text for overlap.

## Results

| Metric | Value |
| --- | --- |
| Peak private memory | 96.32 MiB |
| Private memory delta | 976.56 KiB |
| Peak working set | 115.39 MiB |
| Working set delta | 976.56 KiB |
| Peak paged memory | 976.56 KiB |
| CPU time delta | 1.00 s |
| Samples captured | 2 |

## Observed UI defects

$Notes

## Gate note

This capture is evidence for `docs/desktop-virtualization-trace.md`.
"@ | Set-Content -LiteralPath $OutputPath -NoNewline
'@ | Set-Content -LiteralPath $fakeCapture -Encoding UTF8

    $outputDirectory = Join-Path $tempRoot 'trace-output'
    & powershell -NoProfile -ExecutionPolicy Bypass -File $runner `
        -DurationSeconds 10 `
        -SampleIntervalSeconds 1 `
        -OutputDirectory $outputDirectory `
        -DisplayScaling '100%' `
        -Notes 'none observed' `
        -SkipPrompts `
        -CaptureScriptPath $fakeCapture `
        -ValidatorScriptPath $validator *> $null

    if ($LASTEXITCODE -ne 0) {
        throw "Trace capture runner failed with exit code $LASTEXITCODE."
    }

    foreach ($surface in @('library', 'search', 'playlist')) {
        $report = Join-Path $outputDirectory "desktop-virtualization-$surface.md"
        $samples = Join-Path $outputDirectory "desktop-virtualization-$surface.samples.csv"
        if (-not (Test-Path -LiteralPath $report)) {
            throw "Trace capture runner did not write report for '$surface'."
        }

        if (-not (Test-Path -LiteralPath $samples)) {
            throw "Trace capture runner did not write samples for '$surface'."
        }
    }

    Write-Host 'Desktop virtualization trace capture runner smoke passed.'
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
