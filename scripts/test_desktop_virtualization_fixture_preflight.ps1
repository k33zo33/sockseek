param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$preflight = Join-Path $PSScriptRoot 'validate_desktop_virtualization_fixture.ps1'
if (-not (Test-Path -LiteralPath $preflight)) {
    throw "Desktop virtualization fixture preflight was not found: $preflight"
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("sockseek-desktop-trace-preflight-" + [System.Guid]::NewGuid().ToString('N'))
$configDir = Join-Path $tempRoot 'config'
$mockSoulseekDir = Join-Path $tempRoot 'mock-soulseek'
$fakeSqlite = Join-Path $tempRoot 'fake-sqlite.cmd'
$fakeDesktop = Join-Path $tempRoot 'Sockseek.Desktop.exe'
New-Item -ItemType Directory -Path $configDir | Out-Null
New-Item -ItemType Directory -Path $mockSoulseekDir | Out-Null
Set-Content -LiteralPath (Join-Path $configDir 'sockseek.db') -Value 'fixture placeholder' -NoNewline -Encoding ASCII
Set-Content -LiteralPath $fakeDesktop -Value 'desktop placeholder' -NoNewline -Encoding ASCII

@'
@echo off
echo %* | findstr /C:"CanonicalTracks" >nul
if %errorlevel%==0 (
  echo 5
  exit /b 0
)
echo %* | findstr /C:"PlaylistItems" >nul
if %errorlevel%==0 (
  echo 3
  exit /b 0
)
echo 0
exit /b 0
'@ | Set-Content -LiteralPath $fakeSqlite -Encoding ASCII

function Invoke-Preflight {
    param(
        [int]$MinimumLibraryTracks = 5,
        [int]$MinimumPlaylistItems = 3,
        [int]$MinimumMockSoulseekFiles = 2,
        [string]$MockDirectory = $mockSoulseekDir
    )

    $stdout = Join-Path $tempRoot ("preflight-out-" + [System.Guid]::NewGuid().ToString('N') + ".txt")
    $stderr = Join-Path $tempRoot ("preflight-err-" + [System.Guid]::NewGuid().ToString('N') + ".txt")
    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        "`"$preflight`"",
        '-ConfigDir',
        "`"$configDir`"",
        '-MockSoulseekDir',
        "`"$MockDirectory`"",
        '-DesktopExecutablePath',
        "`"$fakeDesktop`"",
        '-SqlitePath',
        "`"$fakeSqlite`"",
        '-MinimumLibraryTracks',
        $MinimumLibraryTracks,
        '-MinimumPlaylistItems',
        $MinimumPlaylistItems,
        '-MinimumMockSoulseekFiles',
        $MinimumMockSoulseekFiles,
        '-RequireSqliteRowCounts'
    )

    $process = Start-Process -FilePath 'powershell' `
        -ArgumentList ($arguments -join ' ') `
        -Wait `
        -PassThru `
        -NoNewWindow `
        -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr

    [pscustomobject]@{
        ExitCode = $process.ExitCode
        Output = Get-Content -LiteralPath $stdout -Raw
        Error = Get-Content -LiteralPath $stderr -Raw
    }
}

try {
    foreach ($index in 1..2) {
        $directory = Join-Path $mockSoulseekDir ("Trace Artist\Trace Album {0}" -f $index)
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $directory ("trace-{0}.mp3" -f $index)) -Value 'fixture' -NoNewline -Encoding ASCII
    }

    $passingResult = Invoke-Preflight
    if ($passingResult.ExitCode -ne 0) {
        throw "Expected fixture preflight to pass. Error: $($passingResult.Error)"
    }

    if ($passingResult.Output.IndexOf('Desktop virtualization fixture preflight passed.', [System.StringComparison]::Ordinal) -lt 0 -or
        $passingResult.Output.IndexOf('Library tracks: 5', [System.StringComparison]::Ordinal) -lt 0 -or
        $passingResult.Output.IndexOf('Playlist items: 3', [System.StringComparison]::Ordinal) -lt 0 -or
        $passingResult.Output.IndexOf('Mock Soulseek files: 2', [System.StringComparison]::Ordinal) -lt 0) {
        throw 'Expected preflight output to include verified fixture counts.'
    }

    $undersizedResult = Invoke-Preflight -MinimumLibraryTracks 6
    if ($undersizedResult.ExitCode -eq 0) {
        throw 'Expected fixture preflight to fail when database row counts are below the required minimum.'
    }

    $missingMockResult = Invoke-Preflight -MockDirectory (Join-Path $tempRoot 'missing-mock-soulseek')
    if ($missingMockResult.ExitCode -eq 0) {
        throw 'Expected fixture preflight to fail when the mock Soulseek directory is missing.'
    }

    Write-Host 'Desktop virtualization fixture preflight smoke passed.'
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
