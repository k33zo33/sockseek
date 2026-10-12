param(
    [string]$ConfigDir = '',

    [string]$MockSoulseekDir = '',

    [ValidateRange(1, 1000000)]
    [int]$LibraryTrackCount = 100000,

    [ValidateRange(1, 1000000)]
    [int]$PlaylistItemCount = 10000,

    [ValidateRange(1, 1000000)]
    [int]$SearchFileCount = 10000,

    [ValidateRange(1, 10000)]
    [int]$FilesPerDirectory = 250,

    [switch]$Force,

    [switch]$NoBuild
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

function Resolve-PathForWrite {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

function Assert-SafeForcePath {
    param([string]$Path)

    $fullTmp = [System.IO.Path]::GetFullPath((Join-Path $repoRoot '.tmp'))
    if (-not $Path.StartsWith($fullTmp, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove '$Path' with -Force because it is outside '$fullTmp'. Remove it manually or choose a .tmp path."
    }
}

function Reset-FixturePath {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    if (-not $Force) {
        throw "Fixture path already exists: $Path. Re-run with -Force to replace .tmp fixture output."
    }

    Assert-SafeForcePath -Path $Path
    Remove-Item -LiteralPath $Path -Recurse -Force
}

function Set-TemporaryEnvironment {
    param(
        [string]$Name,
        [string]$Value,
        [hashtable]$PreviousValues
    )

    $PreviousValues[$Name] = [Environment]::GetEnvironmentVariable($Name)
    [Environment]::SetEnvironmentVariable($Name, $Value)
}

function Restore-TemporaryEnvironment {
    param([hashtable]$PreviousValues)

    foreach ($name in $PreviousValues.Keys) {
        [Environment]::SetEnvironmentVariable($name, $PreviousValues[$name])
    }
}

function New-MockSoulseekFiles {
    param(
        [string]$Root,
        [int]$Count,
        [int]$PerDirectory
    )

    New-Item -ItemType Directory -Force -Path $Root | Out-Null
    for ($index = 1; $index -le $Count; $index++) {
        $directoryNumber = [int][Math]::Floor(($index - 1) / $PerDirectory)
        $directory = Join-Path $Root ("Trace Artist\Trace Album {0:D4}" -f $directoryNumber)
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
        $file = Join-Path $directory ("{0:D5}. Trace Artist - Trace Search Candidate {0:D5}.mp3" -f $index)
        Set-Content -LiteralPath $file -Value 'sockseek trace fixture' -NoNewline -Encoding ASCII
    }
}

$resolvedConfigDir = Resolve-PathForWrite -Path $ConfigDir
$resolvedMockSoulseekDir = Resolve-PathForWrite -Path $MockSoulseekDir
$summaryPath = Join-Path (Split-Path -Parent $resolvedConfigDir) 'desktop-trace-fixture-readme.md'
$databasePath = Join-Path $resolvedConfigDir 'sockseek.db'

Reset-FixturePath -Path $resolvedConfigDir
Reset-FixturePath -Path $resolvedMockSoulseekDir
New-Item -ItemType Directory -Force -Path $resolvedConfigDir | Out-Null

Write-Host "Creating mock Soulseek search fixture: $resolvedMockSoulseekDir"
New-MockSoulseekFiles -Root $resolvedMockSoulseekDir -Count $SearchFileCount -PerDirectory $FilesPerDirectory

$previous = @{}
try {
    Set-TemporaryEnvironment -Name 'SOCKSEEK_WRITE_DESKTOP_TRACE_FIXTURE' -Value '1' -PreviousValues $previous
    Set-TemporaryEnvironment -Name 'SOCKSEEK_DESKTOP_TRACE_FIXTURE_CONFIG_DIR' -Value $resolvedConfigDir -PreviousValues $previous
    Set-TemporaryEnvironment -Name 'SOCKSEEK_DESKTOP_TRACE_FIXTURE_OVERWRITE' -Value '1' -PreviousValues $previous
    Set-TemporaryEnvironment -Name 'SOCKSEEK_DESKTOP_TRACE_LIBRARY_COUNT' -Value $LibraryTrackCount.ToString() -PreviousValues $previous
    Set-TemporaryEnvironment -Name 'SOCKSEEK_DESKTOP_TRACE_PLAYLIST_COUNT' -Value $PlaylistItemCount.ToString() -PreviousValues $previous

    $dotnetArguments = @(
        'test',
        'Sockseek.Infrastructure.Tests\Sockseek.Infrastructure.Tests.csproj',
        '-c',
        'Release',
        '--filter',
        'FullyQualifiedName~DesktopVirtualizationTraceFixtureWriterTests',
        '--logger',
        'console;verbosity=minimal'
    )
    if ($NoBuild) {
        $dotnetArguments += '--no-build'
    }

    Write-Host "Seeding fixture database: $databasePath"
    & dotnet @dotnetArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Fixture database writer failed with exit code $LASTEXITCODE."
    }
}
finally {
    Restore-TemporaryEnvironment -PreviousValues $previous
}

@"
# Desktop virtualization trace fixture

Generated on $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz').

| Field | Value |
| --- | --- |
| Config dir | ``$resolvedConfigDir`` |
| Database | ``$databasePath`` |
| Library tracks | ``$LibraryTrackCount`` |
| Playlist items | ``$PlaylistItemCount`` |
| Mock Soulseek dir | ``$resolvedMockSoulseekDir`` |
| Mock Soulseek files | ``$SearchFileCount`` |

## Launch environment

Run the Desktop app from this repository with these environment variables set so its development daemon uses the fixture database and mock Soulseek search files:

````powershell
`$env:SOCKSEEK_DESKTOP_CONFIG_DIR = "$resolvedConfigDir"
`$env:SockseekServer__Engine__MockFilesDir = "$resolvedMockSoulseekDir"
`$env:SockseekServer__Engine__MockFilesReadTags = "false"
dotnet run --project Sockseek.Desktop\Sockseek.Desktop.csproj -c Release
````

Then start the daemon from the Desktop UI and capture the three surfaces with `scripts/run_desktop_virtualization_trace_capture.ps1`.
"@ | Set-Content -LiteralPath $summaryPath -Encoding UTF8

Write-Host ''
Write-Host 'Desktop virtualization fixture prepared.'
Write-Host "Config dir: $resolvedConfigDir"
Write-Host "Mock Soulseek dir: $resolvedMockSoulseekDir"
Write-Host "Summary: $summaryPath"
