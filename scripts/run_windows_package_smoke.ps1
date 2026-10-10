param(
    [string]$Version = '3.0.5',

    [string]$Commit = '',

    [string]$SourceUrl = 'https://github.com/k33zo33/sockseek',

    [string]$Runtime = 'win-x64',

    [string]$Configuration = 'Release',

    [string]$ArtifactsRoot = '',

    [string]$DotNetCli = 'dotnet',

    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($Arguments -join ' ')"
    }
}

if ([string]::IsNullOrWhiteSpace($Commit)) {
    $Commit = (& git rev-parse --short HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($Commit)) {
        throw "Unable to resolve the current Git commit. Pass -Commit explicitly."
    }
}

if ([string]::IsNullOrWhiteSpace($ArtifactsRoot)) {
    $ArtifactsRoot = ".tmp\sprint15-package-smoke-$Commit"
}

$repoRoot = (Resolve-Path -LiteralPath '.').Path
$artifactsRootFullPath = [System.IO.Path]::GetFullPath($ArtifactsRoot)
$repositoryPrefix = $repoRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $artifactsRootFullPath.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "ArtifactsRoot must stay inside the repository: $artifactsRootFullPath"
}

if (Test-Path -LiteralPath $artifactsRootFullPath) {
    if (-not $Force) {
        throw "Artifacts root already exists: $artifactsRootFullPath. Pass -Force to replace it."
    }

    Remove-Item -LiteralPath $artifactsRootFullPath -Recurse -Force
}

$desktopPublishDir = Join-Path $artifactsRootFullPath "publish-desktop-$Runtime"
$daemonPublishDir = Join-Path $artifactsRootFullPath "publish-daemon-$Runtime"
$stageDir = Join-Path $artifactsRootFullPath "stage-$Runtime"
$sbomPath = Join-Path $artifactsRootFullPath 'release-sbom.spdx.json'
$archivePath = Join-Path $artifactsRootFullPath "Sockseek-$Runtime.zip"
$manifestPath = Join-Path $artifactsRootFullPath "Sockseek-$Runtime.sha256"
$testerInstructionsFileName = 'closed-beta-tester-instructions.md'
$testerInstructionsPath = Join-Path $stageDir $testerInstructionsFileName

New-Item -ItemType Directory -Force -Path $artifactsRootFullPath | Out-Null

Write-Host "Running Windows package smoke for commit $Commit."
Write-Host "Artifacts root: $artifactsRootFullPath"

Write-Host "Publishing Sockseek.Desktop for $Runtime."
Invoke-CheckedCommand $DotNetCli @(
    'publish',
    'Sockseek.Desktop\Sockseek.Desktop.csproj',
    '-c',
    $Configuration,
    '-r',
    $Runtime,
    '--self-contained',
    'true',
    '-o',
    $desktopPublishDir)

Write-Host "Publishing Sockseek.Server daemon for $Runtime."
Invoke-CheckedCommand $DotNetCli @(
    'publish',
    'Sockseek.Server\Sockseek.Server.csproj',
    '-c',
    $Configuration,
    '-r',
    $Runtime,
    '--self-contained',
    'true',
    '-o',
    $daemonPublishDir)

Write-Host "Generating SPDX SBOM."
Invoke-CheckedCommand $DotNetCli @(
    'run',
    '--project',
    'Sockseek.Packager\Sockseek.Packager.csproj',
    '-c',
    $Configuration,
    '--',
    'generate-sbom',
    $repoRoot,
    $sbomPath,
    'Sockseek',
    $Version,
    $Commit,
    $SourceUrl)

Write-Host "Staging Windows release layout."
Invoke-CheckedCommand $DotNetCli @(
    'run',
    '--project',
    'Sockseek.Packager\Sockseek.Packager.csproj',
    '-c',
    $Configuration,
    '--',
    'stage-windows',
    $repoRoot,
    $desktopPublishDir,
    $daemonPublishDir,
    $stageDir,
    'Sockseek.Desktop.exe',
    'Sockseek.Server.exe',
    $Version,
    $Commit,
    $SourceUrl,
    $sbomPath)

Write-Host "Generating closed beta tester instructions."
Invoke-CheckedCommand 'powershell' @(
    '-NoProfile',
    '-ExecutionPolicy',
    'Bypass',
    '-File',
    'scripts\write_closed_beta_tester_instructions.ps1',
    '-Commit',
    $Commit,
    '-SourceUrl',
    $SourceUrl,
    '-ArtifactName',
    "Sockseek $Runtime closed beta",
    '-Version',
    $Version,
    '-OutputPath',
    $testerInstructionsPath)

if (-not (Test-Path -LiteralPath $testerInstructionsPath)) {
    throw "Closed beta tester instructions were not generated: $testerInstructionsPath"
}

Write-Host "Creating Windows release archive and SHA256 manifest."
Invoke-CheckedCommand $DotNetCli @(
    'run',
    '--project',
    'Sockseek.Packager\Sockseek.Packager.csproj',
    '-c',
    $Configuration,
    '--',
    'archive-windows',
    $stageDir,
    $archivePath,
    $manifestPath,
    'Sockseek.Desktop.exe',
    'Sockseek.Server.exe')

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    if (-not ($archive.Entries | Where-Object { $_.FullName -eq $testerInstructionsFileName })) {
        throw "Windows release archive is missing $testerInstructionsFileName"
    }
}
finally {
    $archive.Dispose()
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw
if ($manifest -notmatch [Regex]::Escape($testerInstructionsFileName)) {
    throw "SHA256 manifest is missing $testerInstructionsFileName"
}

Write-Host "Windows package smoke completed successfully."
Write-Host "Stage: $stageDir"
Write-Host "Archive: $archivePath"
Write-Host "Manifest: $manifestPath"
Write-Host "Tester instructions: $testerInstructionsPath"
