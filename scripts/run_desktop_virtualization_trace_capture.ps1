param(
    [ValidateRange(10, 3600)]
    [int]$DurationSeconds = 180,

    [ValidateRange(1, 60)]
    [int]$SampleIntervalSeconds = 2,

    [string]$ProcessName = 'Sockseek.Desktop',

    [string]$OutputDirectory = 'artifacts',

    [string]$DisplayScaling = '',

    [string]$Notes = 'none observed',

    [switch]$SkipPrompts,

    [string]$CaptureScriptPath = '',

    [string]$ValidatorScriptPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

if ([string]::IsNullOrWhiteSpace($CaptureScriptPath)) {
    $CaptureScriptPath = Join-Path $PSScriptRoot 'capture_desktop_virtualization_trace.ps1'
}

if ([string]::IsNullOrWhiteSpace($ValidatorScriptPath)) {
    $ValidatorScriptPath = Join-Path $PSScriptRoot 'validate_desktop_virtualization_traces.ps1'
}

if (-not (Test-Path -LiteralPath $CaptureScriptPath)) {
    throw "Capture helper was not found: $CaptureScriptPath"
}

if (-not (Test-Path -LiteralPath $ValidatorScriptPath)) {
    throw "Trace validator was not found: $ValidatorScriptPath"
}

$resolvedOutputDirectory = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory
} else {
    Join-Path $repoRoot $OutputDirectory
}

$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($resolvedOutputDirectory)
New-Item -ItemType Directory -Force -Path $resolvedOutputDirectory | Out-Null

$surfaces = @(
    [pscustomobject]@{
        Name = 'library'
        FixtureSize = 100000
        Preparation = 'Load the Library view with 100,000 indexed canonical tracks and the first result page visible.'
    }
    [pscustomobject]@{
        Name = 'search'
        FixtureSize = 10000
        Preparation = 'Load the Search view with 10,000 Soulseek file candidates visible.'
    }
    [pscustomobject]@{
        Name = 'playlist'
        FixtureSize = 10000
        Preparation = 'Load the Playlists view with a 10,000 item imported playlist selected.'
    }
)

$reports = @{}

function Invoke-TraceCapture {
    param(
        [pscustomobject]$Surface
    )

    $reportPath = Join-Path $resolvedOutputDirectory "desktop-virtualization-$($Surface.Name).md"
    $samplesPath = Join-Path $resolvedOutputDirectory "desktop-virtualization-$($Surface.Name).samples.csv"

    Write-Host ''
    Write-Host "Preparing $($Surface.Name) rendered trace."
    Write-Host $Surface.Preparation
    Write-Host 'During capture: scroll top-to-bottom and back, resize narrow/wide, and check row actions and text overlap.'

    if (-not $SkipPrompts) {
        Read-Host 'Press Enter when the rendered Desktop surface is ready'
    }

    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        "`"$CaptureScriptPath`"",
        '-Surface',
        $Surface.Name,
        '-FixtureSize',
        $Surface.FixtureSize,
        '-DurationSeconds',
        $DurationSeconds,
        '-SampleIntervalSeconds',
        $SampleIntervalSeconds,
        '-ProcessName',
        "`"$ProcessName`"",
        '-OutputPath',
        "`"$reportPath`"",
        '-SamplesCsvPath',
        "`"$samplesPath`"",
        '-Notes',
        "`"$Notes`""
    )

    if (-not [string]::IsNullOrWhiteSpace($DisplayScaling)) {
        $arguments += @('-DisplayScaling', "`"$DisplayScaling`"")
    }

    $process = Start-Process -FilePath 'powershell' `
        -ArgumentList ($arguments -join ' ') `
        -Wait `
        -PassThru `
        -NoNewWindow

    if ($process.ExitCode -ne 0) {
        throw "Capture for surface '$($Surface.Name)' failed with exit code $($process.ExitCode)."
    }

    $reports[$Surface.Name] = $reportPath
}

Write-Host 'Desktop virtualization trace capture runner'
Write-Host "Output directory: $resolvedOutputDirectory"
Write-Host 'Start the packaged Desktop app and load each requested fixture before its capture starts.'

foreach ($surface in $surfaces) {
    Invoke-TraceCapture -Surface $surface
}

$validatorArguments = @(
    '-NoProfile',
    '-ExecutionPolicy',
    'Bypass',
    '-File',
    "`"$ValidatorScriptPath`"",
    '-LibraryReportPath',
    "`"$($reports['library'])`"",
    '-SearchReportPath',
    "`"$($reports['search'])`"",
    '-PlaylistReportPath',
    "`"$($reports['playlist'])`""
)

$validatorProcess = Start-Process -FilePath 'powershell' `
    -ArgumentList ($validatorArguments -join ' ') `
    -Wait `
    -PassThru `
    -NoNewWindow

if ($validatorProcess.ExitCode -ne 0) {
    throw "Desktop virtualization trace validation failed with exit code $($validatorProcess.ExitCode)."
}

Write-Host ''
Write-Host 'Desktop virtualization trace capture and validation completed.'
foreach ($surface in $surfaces) {
    Write-Host "$($surface.Name): $($reports[$surface.Name])"
}
