param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactPath,

    [Parameter(Mandatory = $true)]
    [string]$EvidencePath,

    [ValidateSet('Any', 'Ubuntu2404X64')]
    [string]$RequiredHost = 'Any',

    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$ExpectedCommit = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-ReleaseMetadata {
    param([string]$Path)

    $json = & tar -xOf $Path 'release-metadata.json' 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($json -join "`n"))) {
        throw 'The package does not contain a readable release-metadata.json at its root.'
    }

    return ($json -join "`n") | ConvertFrom-Json
}

function Get-HostClassification {
    param($HostEvidence)

    $distributionId = [string]$HostEvidence.distributionId
    $distributionVersion = [string]$HostEvidence.distributionVersion
    $architecture = [string]$HostEvidence.architecture
    $isX64 = $architecture -match '^(?i:x86_64|amd64|x64)$'

    if ($distributionId -eq 'ubuntu' -and $distributionVersion -match '^24[.]04(?:[.]|$)' -and $isX64) {
        return 'Ubuntu2404X64'
    }

    return 'Other'
}

$resolvedArtifact = (Resolve-Path -LiteralPath $ArtifactPath).Path
$resolvedEvidence = (Resolve-Path -LiteralPath $EvidencePath).Path
if (-not $resolvedArtifact.EndsWith('.tar.gz', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Ubuntu package evidence requires a .tar.gz artifact.'
}

$release = Read-ReleaseMetadata -Path $resolvedArtifact
$evidence = Get-Content -LiteralPath $resolvedEvidence -Raw | ConvertFrom-Json
$artifactName = [IO.Path]::GetFileName($resolvedArtifact)
$artifactHash = (Get-FileHash -LiteralPath $resolvedArtifact -Algorithm SHA256).Hash.ToLowerInvariant()
$releaseCommit = ([string]$release.commit).ToLowerInvariant()
$evidenceCommit = ([string]$evidence.commit).ToLowerInvariant()

if ($evidence.schemaVersion -ne 1) {
    throw "Unsupported Ubuntu package evidence schema version: $($evidence.schemaVersion)"
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
if ([string]$evidence.runtime -ne 'linux-x64') {
    throw "Evidence runtime must be linux-x64, found '$($evidence.runtime)'."
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
    throw "Evidence requires host $RequiredHost, but the recorded host is ${hostClassification}: $($evidence.host.distributionId) $($evidence.host.distributionVersion) $($evidence.host.architecture)."
}

$requiredChecks = @(
    'archiveExtracted',
    'requiredPayloadPresent',
    'executableModes',
    'daemonHealth',
    'daemonProtectedApiRejectsAnonymous',
    'daemonSoulseekAccountLifecycle',
    'daemonSoulseekAccountRestartRestore',
    'daemonSoulseekSecretRemovedAfterDisconnect',
    'secretPermissions',
    'secretEncryptedAtRest',
    'desktopStartedPackagedDaemon',
    'desktopDaemonHealth',
    'desktopDaemonProtectedApiRejectsAnonymous',
    'loopbackOnly',
    'installUninstall',
    'userDataPreserved'
)
foreach ($check in $requiredChecks) {
    $property = $evidence.checks.PSObject.Properties[$check]
    if ($null -eq $property -or $property.Value -ne $true) {
        throw "Ubuntu package evidence check did not pass: $check"
    }
}

Write-Host 'Ubuntu package smoke evidence validation passed.'
Write-Host "Artifact commit: $releaseCommit"
Write-Host "Artifact SHA256: $artifactHash"
Write-Host "Recorded host: $hostClassification"
