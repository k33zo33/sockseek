param(
    [string]$ConfigDir = '',

    [string]$MockSoulseekDir = '',

    [string]$DesktopExecutablePath = '',

    [string]$SqlitePath = '',

    [ValidateRange(1, 1000000)]
    [int]$MinimumLibraryTracks = 100000,

    [ValidateRange(1, 1000000)]
    [int]$MinimumPlaylistItems = 10000,

    [ValidateRange(1, 1000000)]
    [int]$MinimumMockSoulseekFiles = 10000,

    [switch]$RequireSqliteRowCounts
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

function Resolve-SqliteExecutable {
    param([string]$Path)

    if (-not [string]::IsNullOrWhiteSpace($Path)) {
        $resolvedPath = Resolve-RepoPath -Path $Path
        Assert-PathExists -Path $resolvedPath -Description 'sqlite3 executable'
        return $resolvedPath
    }

    $command = Get-Command sqlite3 -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    return ''
}

function Invoke-SqliteScalar {
    param(
        [string]$ExecutablePath,
        [string]$DatabasePath,
        [string]$Sql
    )

    $output = & $ExecutablePath $DatabasePath $Sql
    if ($LASTEXITCODE -ne 0) {
        throw "sqlite3 failed with exit code $LASTEXITCODE for query: $Sql"
    }

    $valueText = ($output | Select-Object -First 1).Trim()
    $value = 0
    if (-not [int]::TryParse($valueText, [ref]$value)) {
        throw "sqlite3 returned a non-integer scalar '$valueText' for query: $Sql"
    }

    return $value
}

function Assert-MinimumCount {
    param(
        [string]$Name,
        [int]$Actual,
        [int]$Minimum
    )

    if ($Actual -lt $Minimum) {
        throw "$Name count $Actual is below required minimum $Minimum."
    }
}

$resolvedConfigDir = Resolve-RepoPath -Path $ConfigDir
$resolvedMockSoulseekDir = Resolve-RepoPath -Path $MockSoulseekDir
$databasePath = Join-Path $resolvedConfigDir 'sockseek.db'

Assert-PathExists -Path $resolvedConfigDir -Description 'Desktop trace config directory'
Assert-PathExists -Path $databasePath -Description 'Desktop trace fixture database'
Assert-PathExists -Path $resolvedMockSoulseekDir -Description 'Mock Soulseek directory'

$databaseInfo = Get-Item -LiteralPath $databasePath
if ($databaseInfo.Length -le 0) {
    throw "Desktop trace fixture database is empty: $databasePath"
}

$mockFileCount = @(Get-ChildItem -LiteralPath $resolvedMockSoulseekDir -File -Recurse).Count
Assert-MinimumCount -Name 'Mock Soulseek file' -Actual $mockFileCount -Minimum $MinimumMockSoulseekFiles

if (-not [string]::IsNullOrWhiteSpace($DesktopExecutablePath)) {
    $resolvedDesktopExecutablePath = Resolve-RepoPath -Path $DesktopExecutablePath
    Assert-PathExists -Path $resolvedDesktopExecutablePath -Description 'Desktop executable'
    Write-Host "Desktop executable: $resolvedDesktopExecutablePath"
}
else {
    Write-Host 'Desktop executable: not provided; public-beta rendered trace should pass -DesktopExecutablePath for the packaged artifact.'
}

$sqliteExecutable = Resolve-SqliteExecutable -Path $SqlitePath
if ([string]::IsNullOrWhiteSpace($sqliteExecutable)) {
    if ($RequireSqliteRowCounts) {
        throw 'sqlite3 was not found. Install sqlite3 or pass -SqlitePath so fixture row counts can be verified.'
    }

    Write-Host 'sqlite3: not found; database row counts were not verified.'
}
else {
    $libraryTracks = Invoke-SqliteScalar -ExecutablePath $sqliteExecutable -DatabasePath $databasePath -Sql 'SELECT COUNT(*) FROM CanonicalTracks;'
    $playlistItems = Invoke-SqliteScalar -ExecutablePath $sqliteExecutable -DatabasePath $databasePath -Sql 'SELECT COUNT(*) FROM PlaylistItems;'

    Assert-MinimumCount -Name 'Library track' -Actual $libraryTracks -Minimum $MinimumLibraryTracks
    Assert-MinimumCount -Name 'Playlist item' -Actual $playlistItems -Minimum $MinimumPlaylistItems

    Write-Host "sqlite3: $sqliteExecutable"
    Write-Host "Library tracks: $libraryTracks"
    Write-Host "Playlist items: $playlistItems"
}

Write-Host "Config dir: $resolvedConfigDir"
Write-Host "Database: $databasePath"
Write-Host "Mock Soulseek dir: $resolvedMockSoulseekDir"
Write-Host "Mock Soulseek files: $mockFileCount"
Write-Host 'Desktop virtualization fixture preflight passed.'
