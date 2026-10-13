param(
    [string]$ConfigDir = '',

    [string]$MockSoulseekDir = '',

    [string]$DesktopExecutablePath = '',

    [switch]$PrepareFixture,

    [switch]$Force,

    [switch]$PrintOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$defaultRoot = Join-Path $repoRoot '.tmp\sprint15-desktop-trace-fixture'

if ([string]::IsNullOrWhiteSpace($ConfigDir)) {
    $ConfigDir = Join-Path $defaultRoot 'config'
}

if ([string]::IsNullOrWhiteSpace($MockSoulseekDir)) {
    $MockSoulseekDir = Join-Path $defaultRoot 'mock-soulseek'
}

function Resolve-RepoPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

function Assert-PathExists {
    param(
        [string]$Path,
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description was not found: $Path"
    }
}

$resolvedConfigDir = Resolve-RepoPath -Path $ConfigDir
$resolvedMockSoulseekDir = Resolve-RepoPath -Path $MockSoulseekDir
$databasePath = Join-Path $resolvedConfigDir 'sockseek.db'

if ($PrepareFixture) {
    $prepareScript = Join-Path $PSScriptRoot 'prepare_desktop_virtualization_fixture.ps1'
    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        "`"$prepareScript`"",
        '-ConfigDir',
        "`"$resolvedConfigDir`"",
        '-MockSoulseekDir',
        "`"$resolvedMockSoulseekDir`""
    )

    if ($Force) {
        $arguments += '-Force'
    }

    $process = Start-Process -FilePath 'powershell' `
        -ArgumentList ($arguments -join ' ') `
        -Wait `
        -PassThru `
        -NoNewWindow

    if ($process.ExitCode -ne 0) {
        throw "Fixture preparation failed with exit code $($process.ExitCode)."
    }
}

Assert-PathExists -Path $resolvedConfigDir -Description 'Desktop trace config directory'
Assert-PathExists -Path $databasePath -Description 'Desktop trace fixture database'
Assert-PathExists -Path $resolvedMockSoulseekDir -Description 'Mock Soulseek directory'

$launchMode = 'developer rehearsal'
$fileName = 'dotnet'
$argumentsText = "run --project `"$repoRoot\Sockseek.Desktop\Sockseek.Desktop.csproj`" -c Release --no-build"
$workingDirectory = $repoRoot

if (-not [string]::IsNullOrWhiteSpace($DesktopExecutablePath)) {
    $resolvedDesktopExecutablePath = Resolve-RepoPath -Path $DesktopExecutablePath
    Assert-PathExists -Path $resolvedDesktopExecutablePath -Description 'Desktop executable'

    $launchMode = 'packaged executable'
    $fileName = $resolvedDesktopExecutablePath
    $argumentsText = ''
    $workingDirectory = Split-Path -Parent $resolvedDesktopExecutablePath
}

Write-Host 'Desktop virtualization fixture launch'
Write-Host "Launch mode: $launchMode"
Write-Host "Working directory: $workingDirectory"
Write-Host "Executable: $fileName"
if (-not [string]::IsNullOrWhiteSpace($argumentsText)) {
    Write-Host "Arguments: $argumentsText"
}

if ($launchMode -eq 'developer rehearsal') {
    Write-Host 'Developer rehearsal launch is useful for checking the fixture, but public-beta rendered trace evidence should use a packaged Desktop executable.'
}

Write-Host "SOCKSEEK_DESKTOP_CONFIG_DIR=$resolvedConfigDir"
Write-Host "SockseekServer__Engine__MockFilesDir=$resolvedMockSoulseekDir"
Write-Host 'SockseekServer__Engine__MockFilesReadTags=false'

if ($PrintOnly) {
    Write-Host 'PrintOnly set; Desktop was not launched.'
    return
}

$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $fileName
$startInfo.Arguments = $argumentsText
$startInfo.WorkingDirectory = $workingDirectory
$startInfo.UseShellExecute = $false
$startInfo.Environment['SOCKSEEK_DESKTOP_CONFIG_DIR'] = $resolvedConfigDir
$startInfo.Environment['SockseekServer__Engine__MockFilesDir'] = $resolvedMockSoulseekDir
$startInfo.Environment['SockseekServer__Engine__MockFilesReadTags'] = 'false'

$started = [System.Diagnostics.Process]::Start($startInfo)
if ($null -eq $started) {
    throw 'Desktop process did not start.'
}

Write-Host "Started Desktop process PID $($started.Id)."
Write-Host 'Load the library, search and playlist surfaces, then run scripts/run_desktop_virtualization_trace_capture.ps1.'
