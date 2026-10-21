param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactPath,

    [Parameter(Mandatory = $true)]
    [string]$EvidencePath,

    [ValidateSet('Any', 'Windows11X64')]
    [string]$RequiredHost = 'Any',

    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$ExpectedCommit = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-ReleaseMetadata {
    param([string]$Path)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entry = $archive.Entries |
            Where-Object { $_.FullName.TrimStart('/', '\') -eq 'release-metadata.json' } |
            Select-Object -First 1
        if ($null -eq $entry) {
            throw 'The package does not contain release-metadata.json at its root.'
        }

        $reader = New-Object System.IO.StreamReader($entry.Open(), [Text.Encoding]::UTF8)
        try {
            return $reader.ReadToEnd() | ConvertFrom-Json
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Get-HostClassification {
    param($HostEvidence)

    $caption = [string]$HostEvidence.caption
    $architecture = [string]$HostEvidence.osArchitecture
    $buildNumber = 0
    $hasBuildNumber = [int]::TryParse([string]$HostEvidence.buildNumber, [ref]$buildNumber)
    $isX64 = $architecture -match '(?i)(64-bit|x64|amd64)'

    if ($caption -match '(?i)Windows 11' -and $isX64 -and $hasBuildNumber -and $buildNumber -ge 22000) {
        return 'Windows11X64'
    }

    if ($caption -match '(?i)Windows 10' -and $isX64 -and $hasBuildNumber) {
        return 'Windows10X64'
    }

    return 'Other'
}

$resolvedArtifact = (Resolve-Path -LiteralPath $ArtifactPath).Path
$resolvedEvidence = (Resolve-Path -LiteralPath $EvidencePath).Path
if (-not $resolvedArtifact.EndsWith('.zip', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Windows package evidence requires a .zip artifact.'
}

$release = Read-ReleaseMetadata -Path $resolvedArtifact
$evidence = Get-Content -LiteralPath $resolvedEvidence -Raw | ConvertFrom-Json
$artifactName = [IO.Path]::GetFileName($resolvedArtifact)
$artifactHash = (Get-FileHash -LiteralPath $resolvedArtifact -Algorithm SHA256).Hash.ToLowerInvariant()
$releaseCommit = ([string]$release.commit).ToLowerInvariant()
$evidenceCommit = ([string]$evidence.commit).ToLowerInvariant()

if ($evidence.schemaVersion -ne 1) {
    throw "Unsupported Windows package evidence schema version: $($evidence.schemaVersion)"
}
if ($releaseCommit -notmatch '^[0-9a-f]{40}$') {
    throw 'release-metadata.json must contain a full 40-character Git commit SHA.'
}
if ($evidenceCommit -ne $releaseCommit) {
    throw 'Evidence commit does not match release-metadata.json in the artifact.'
}
if ($ExpectedCommit -and $releaseCommit -ne $ExpectedCommit.ToLowerInvariant()) {
    throw "Artifact commit $releaseCommit does not match expected commit $($ExpectedCommit.ToLowerInvariant())."
}
if ([string]$evidence.version -ne [string]$release.version) {
    throw 'Evidence version does not match release-metadata.json in the artifact.'
}
if ([string]$release.license -ne 'AGPL-3.0') {
    throw "Artifact license must be AGPL-3.0, found '$($release.license)'."
}
if ([string]$evidence.runtime -ne 'win-x64') {
    throw "Evidence runtime must be win-x64, found '$($evidence.runtime)'."
}
if ([string]$evidence.archive -ne $artifactName) {
    throw "Evidence names artifact '$($evidence.archive)', but '$artifactName' was supplied."
}
if ([string]$evidence.archiveSha256 -ne $artifactHash) {
    throw 'Artifact SHA256 does not match the package smoke evidence.'
}

$recordedAt = [DateTimeOffset]::MinValue
if (-not [DateTimeOffset]::TryParse([string]$evidence.recordedAtUtc, [ref]$recordedAt)) {
    throw 'Evidence recordedAtUtc is not a valid timestamp.'
}

$hostClassification = Get-HostClassification -HostEvidence $evidence.host
if ($RequiredHost -ne 'Any' -and $hostClassification -ne $RequiredHost) {
    throw "Evidence requires host $RequiredHost, but the recorded host is ${hostClassification}: $($evidence.host.caption) $($evidence.host.version) build $($evidence.host.buildNumber) $($evidence.host.osArchitecture)."
}

$requiredChecks = @(
    'archiveExtracted',
    'daemonHealth',
    'daemonProtectedApiRejectsAnonymous',
    'daemonSoulseekAccountLifecycle',
    'daemonSoulseekAccountRestartRestore',
    'daemonSoulseekSecretRemovedAfterDisconnect',
    'desktopStartedPackagedDaemon',
    'desktopDaemonHealth',
    'desktopDaemonProtectedApiRejectsAnonymous',
    'loopbackOnly'
)
foreach ($check in $requiredChecks) {
    $property = $evidence.checks.PSObject.Properties[$check]
    if ($null -eq $property -or $property.Value -ne $true) {
        throw "Windows package evidence check did not pass: $check"
    }
}

Write-Host 'Windows package smoke evidence validation passed.'
Write-Host "Artifact commit: $releaseCommit"
Write-Host "Artifact SHA256: $artifactHash"
Write-Host "Recorded host: $hostClassification"
