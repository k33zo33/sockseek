param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$validator = Join-Path $PSScriptRoot 'validate_desktop_virtualization_traces.ps1'
if (-not (Test-Path -LiteralPath $validator)) {
    throw "Validator script was not found: $validator"
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("sockseek-desktop-trace-validator-" + [System.Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

function Write-Samples {
    param(
        [string]$Surface
    )

    $path = Join-Path $tempRoot "$Surface.samples.csv"
    @"
"TimestampUtc","PrivateBytes","WorkingSetBytes","PagedMemoryBytes","CpuSeconds"
"2026-10-10T10:00:00.0000000Z","100000000","120000000","1000000","1"
"2026-10-10T10:00:02.0000000Z","101000000","121000000","1000000","2"
"@ | Set-Content -LiteralPath $path -NoNewline

    return $path
}

function Write-TraceReport {
    param(
        [string]$Surface,
        [int]$FixtureSize,
        [string]$Commit = 'abc1234',
        [bool]$MainWindowPresent = $true,
        [bool]$HeadlessProcessAllowed = $false,
        [string]$Defects = 'none observed'
    )

    $samplesPath = Write-Samples -Surface $Surface
    $reportPath = Join-Path $tempRoot "$Surface.md"
    @"
# Desktop virtualization trace capture

Recorded on 2026-10-10 10:00:00 +02:00.

## Metadata

| Field | Value |
| --- | --- |
| Commit | ``$Commit`` |
| Surface | ``$Surface`` |
| Fixture size | ``$FixtureSize`` |
| Process | ``Sockseek.Desktop`` PID ``1234`` |
| Process start time | ``2026-10-10T09:59:00.0000000+02:00`` |
| Main window title | ``Sockseek`` |
| Main window present | ``$MainWindowPresent`` |
| Headless process allowed | ``$HeadlessProcessAllowed`` |
| OS | Windows test fixture |
| Display scaling | 100% |
| Duration | 10 seconds |
| Sample interval | 2 seconds |
| Raw samples | ``$samplesPath`` |

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

$Defects

## Gate note

This capture is evidence for `docs/desktop-virtualization-trace.md`.
"@ | Set-Content -LiteralPath $reportPath -NoNewline

    return $reportPath
}

function Invoke-Validator {
    param(
        [string]$LibraryReport,
        [string]$SearchReport,
        [string]$PlaylistReport
    )

    $stdout = Join-Path $tempRoot ("validator-out-" + [System.Guid]::NewGuid().ToString('N') + ".txt")
    $stderr = Join-Path $tempRoot ("validator-err-" + [System.Guid]::NewGuid().ToString('N') + ".txt")
    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        "`"$validator`"",
        '-LibraryReportPath',
        "`"$LibraryReport`"",
        '-SearchReportPath',
        "`"$SearchReport`"",
        '-PlaylistReportPath',
        "`"$PlaylistReport`""
    ) -join ' '

    $process = Start-Process -FilePath 'powershell' `
        -ArgumentList $arguments `
        -Wait `
        -PassThru `
        -NoNewWindow `
        -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr

    return [pscustomobject]@{
        ExitCode = $process.ExitCode
        Output = if (Test-Path -LiteralPath $stdout) { Get-Content -LiteralPath $stdout -Raw } else { '' }
        Error = if (Test-Path -LiteralPath $stderr) { Get-Content -LiteralPath $stderr -Raw } else { '' }
    }
}

try {
    $library = Write-TraceReport -Surface 'library' -FixtureSize 100000
    $search = Write-TraceReport -Surface 'search' -FixtureSize 10000
    $playlist = Write-TraceReport -Surface 'playlist' -FixtureSize 10000
    $validResult = Invoke-Validator -LibraryReport $library -SearchReport $search -PlaylistReport $playlist
    if ($validResult.ExitCode -ne 0) {
        throw "Expected valid rendered trace reports to pass validation.`n$($validResult.Output)`n$($validResult.Error)"
    }

    $headlessSearch = Write-TraceReport -Surface 'search' -FixtureSize 10000 -HeadlessProcessAllowed $true
    if ((Invoke-Validator -LibraryReport $library -SearchReport $headlessSearch -PlaylistReport $playlist).ExitCode -eq 0) {
        throw 'Expected validation to reject a report captured with headless process allowance.'
    }

    $smallPlaylist = Write-TraceReport -Surface 'playlist' -FixtureSize 9999
    if ((Invoke-Validator -LibraryReport $library -SearchReport $search -PlaylistReport $smallPlaylist).ExitCode -eq 0) {
        throw 'Expected validation to reject a playlist trace below the required fixture size.'
    }

    $otherCommitPlaylist = Write-TraceReport -Surface 'playlist' -FixtureSize 10000 -Commit 'def5678'
    if ((Invoke-Validator -LibraryReport $library -SearchReport $search -PlaylistReport $otherCommitPlaylist).ExitCode -eq 0) {
        throw 'Expected validation to reject traces captured from different commits.'
    }

    Write-Host 'Desktop virtualization trace validator smoke passed.'
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
