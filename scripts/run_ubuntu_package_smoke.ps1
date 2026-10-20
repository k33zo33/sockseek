param(
    [string]$Version = '3.0.5',

    [string]$Commit = '',

    [string]$SourceUrl = 'https://github.com/k33zo33/sockseek',

    [string]$Runtime = 'linux-x64',

    [string]$Configuration = 'Release',

    [string]$ArtifactsRoot = '',

    [string]$DockerCli = 'docker',

    [string]$DotNetSdkImage = 'mcr.microsoft.com/dotnet/sdk:10.0-noble',

    [string]$SmokeImageName = 'sockseek:ubuntu-24.04-package-smoke',

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

$repoRoot = (Resolve-Path -LiteralPath '.').Path
$headCommit = (& git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($headCommit)) {
    throw 'Unable to resolve the current Git commit.'
}

if ([string]::IsNullOrWhiteSpace($Commit)) {
    $Commit = $headCommit
}
elseif (-not $headCommit.StartsWith($Commit, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Requested commit $Commit is not the checked-out commit $headCommit."
}

$workingTreeChanges = @(& git status --porcelain --untracked-files=all)
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to inspect the Git working tree.'
}
if ($workingTreeChanges.Count -gt 0) {
    throw 'Ubuntu package smoke requires a clean working tree so the artifact matches its recorded commit.'
}

if ([string]::IsNullOrWhiteSpace($ArtifactsRoot)) {
    $ArtifactsRoot = ".tmp\sprint16-ubuntu-package-smoke-$($headCommit.Substring(0, 7))"
}

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

New-Item -ItemType Directory -Force -Path $artifactsRootFullPath | Out-Null
$artifactsRelativePath = $artifactsRootFullPath.Substring($repositoryPrefix.Length).Replace('\', '/')
$containerArtifactsRoot = "/src/$artifactsRelativePath"
$desktopPublishDir = "$containerArtifactsRoot/publish-desktop-$Runtime"
$daemonPublishDir = "$containerArtifactsRoot/publish-daemon-$Runtime"
$desktopBuildArtifacts = "$containerArtifactsRoot/build-desktop"
$daemonBuildArtifacts = "$containerArtifactsRoot/build-daemon"
$packagerBuildArtifacts = "$containerArtifactsRoot/build-packager"
$packagerPublishDir = "$containerArtifactsRoot/publish-packager"
$packagerDll = "$packagerPublishDir/Sockseek.Packager.dll"
$stageDir = "$containerArtifactsRoot/stage-$Runtime"
$sbomPath = "$containerArtifactsRoot/release-sbom.spdx.json"
$archivePath = "$containerArtifactsRoot/Sockseek-$Runtime.tar.gz"
$manifestPath = "$containerArtifactsRoot/Sockseek-$Runtime.sha256"
$hostStageDir = Join-Path $artifactsRootFullPath "stage-$Runtime"
$hostArchivePath = Join-Path $artifactsRootFullPath "Sockseek-$Runtime.tar.gz"
$hostManifestPath = Join-Path $artifactsRootFullPath "Sockseek-$Runtime.sha256"
$testerInstructionsPath = Join-Path $hostStageDir 'closed-beta-tester-instructions.md'
$repoMount = "type=bind,source=$repoRoot,target=/src"

Write-Host "Running Ubuntu 24.04 package smoke for commit $headCommit."
Write-Host "Artifacts root: $artifactsRootFullPath"

Invoke-CheckedCommand $DockerCli @('version')

Write-Host "Publishing Sockseek.Desktop for $Runtime in Ubuntu SDK container."
Invoke-CheckedCommand $DockerCli @(
    'run', '--rm', '--mount', $repoMount, '--workdir', '/src', $DotNetSdkImage,
    'dotnet', 'publish', 'Sockseek.Desktop/Sockseek.Desktop.csproj',
    '-c', $Configuration, '-r', $Runtime, '--self-contained', 'true',
    '--artifacts-path', $desktopBuildArtifacts, '-o', $desktopPublishDir)

Write-Host "Publishing Sockseek.Server for $Runtime in Ubuntu SDK container."
Invoke-CheckedCommand $DockerCli @(
    'run', '--rm', '--mount', $repoMount, '--workdir', '/src', $DotNetSdkImage,
    'dotnet', 'publish', 'Sockseek.Server/Sockseek.Server.csproj',
    '-c', $Configuration, '-r', $Runtime, '--self-contained', 'true',
    '--artifacts-path', $daemonBuildArtifacts, '-o', $daemonPublishDir)

Write-Host 'Publishing release packager with isolated build artifacts.'
Invoke-CheckedCommand $DockerCli @(
    'run', '--rm', '--mount', $repoMount, '--workdir', '/src', $DotNetSdkImage,
    'dotnet', 'publish', 'Sockseek.Packager/Sockseek.Packager.csproj',
    '-c', $Configuration, '--artifacts-path', $packagerBuildArtifacts, '-o', $packagerPublishDir)

Write-Host 'Generating SPDX SBOM in Ubuntu SDK container.'
Invoke-CheckedCommand $DockerCli @(
    'run', '--rm', '--mount', $repoMount, '--workdir', '/src', $DotNetSdkImage,
    'dotnet', $packagerDll,
    'generate-sbom', '/src', $sbomPath, 'Sockseek', $Version, $headCommit, $SourceUrl)

Write-Host 'Staging Linux release layout.'
Invoke-CheckedCommand $DockerCli @(
    'run', '--rm', '--mount', $repoMount, '--workdir', '/src', $DotNetSdkImage,
    'dotnet', $packagerDll,
    'stage-linux', '/src', $desktopPublishDir, $daemonPublishDir, $stageDir,
    'Sockseek.Desktop', 'Sockseek.Server', $Version, $headCommit, $SourceUrl, $sbomPath)

Write-Host 'Generating closed beta tester instructions.'
Invoke-CheckedCommand 'powershell.exe' @(
    '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'scripts/write_closed_beta_tester_instructions.ps1',
    '-Commit', $headCommit,
    '-SourceUrl', $SourceUrl,
    '-ArtifactName', "Sockseek Ubuntu 24.04 $Runtime closed beta",
    '-Version', $Version,
    '-OutputPath', $testerInstructionsPath)

Write-Host 'Creating Linux tar.gz and SHA256 manifest.'
Invoke-CheckedCommand $DockerCli @(
    'run', '--rm', '--mount', $repoMount, '--workdir', '/src', $DotNetSdkImage,
    'dotnet', $packagerDll,
    'archive-linux', $stageDir, $archivePath, $manifestPath, 'Sockseek.Desktop', 'Sockseek.Server')

$manifest = Get-Content -LiteralPath $hostManifestPath -Raw
foreach ($requiredEntry in @(
    "Sockseek-$Runtime.tar.gz",
    'Sockseek.Desktop',
    'daemon/Sockseek.Server',
    'closed-beta-tester-instructions.md',
    'SECURITY.md',
    'THIRD-PARTY-NOTICES',
    'docs/beta-limitations.md',
    'release-metadata.json',
    'sbom.spdx.json')) {
    if ($manifest.IndexOf($requiredEntry, [StringComparison]::Ordinal) -lt 0) {
        throw "SHA256 manifest is missing $requiredEntry"
    }
}

Write-Host "Building Ubuntu 24.04 runtime smoke image $SmokeImageName."
Invoke-CheckedCommand $DockerCli @(
    'build', '-t', $SmokeImageName,
    '-f', 'docker/ubuntu-smoke/Dockerfile',
    'docker/ubuntu-smoke')

Write-Host 'Running exact extracted package smoke.'
$archiveMount = "type=bind,source=$hostArchivePath,target=/artifacts/Sockseek-$Runtime.tar.gz,readonly"
Invoke-CheckedCommand $DockerCli @(
    'run', '--rm', '--mount', $archiveMount,
    $SmokeImageName, "/artifacts/Sockseek-$Runtime.tar.gz")

$archiveHash = (Get-FileHash -LiteralPath $hostArchivePath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host 'Ubuntu 24.04 package smoke completed successfully.'
Write-Host "Commit: $headCommit"
Write-Host "Archive: $hostArchivePath"
Write-Host "Archive SHA256: $archiveHash"
Write-Host "Manifest: $hostManifestPath"
Write-Host "Stage: $hostStageDir"
