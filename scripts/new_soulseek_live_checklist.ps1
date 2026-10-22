param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactPath,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Reviewer,

    [ValidatePattern('^[A-Za-z0-9._-]{3,64}$')]
    [string]$SessionLabel = '',

    [ValidateSet('Soulseek enabled', 'Soulseek disabled for public distribution')]
    [string]$NetworkMode = 'Soulseek enabled',

    [ValidateSet('Any', 'Windows11X64', 'Ubuntu2404X64')]
    [string]$RequiredHost = 'Any',

    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$ExpectedCommit = '',

    [string]$OutputRoot = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-ReleaseMetadata {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if ($Path.EndsWith('.zip', [StringComparison]::OrdinalIgnoreCase)) {
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

    if ($Path.EndsWith('.tar.gz', [StringComparison]::OrdinalIgnoreCase)) {
        $json = & tar -xOf $Path 'release-metadata.json'
        if ($LASTEXITCODE -ne 0) {
            throw "Could not read release-metadata.json from package: $Path"
        }

        return ($json -join [Environment]::NewLine) | ConvertFrom-Json
    }

    throw 'Artifact must be a Sockseek .zip or .tar.gz package.'
}

function Get-HostClassification {
    param(
        [string]$Family,
        [string]$Description,
        [string]$Version,
        [string]$Architecture
    )

    $isX64 = $Architecture -match '^(?i:X64|AMD64|x86_64)$'
    if ($Family -eq 'Windows') {
        $buildNumber = -1
        $buildMatch = [regex]::Match($Version, '(?i)\bbuild\s+(?<value>\d+)\b')
        if ($buildMatch.Success) {
            $buildNumber = [int]$buildMatch.Groups['value'].Value
        }
        else {
            $versionValue = $Version.Split(' ', 2)[0]
            $parsedVersion = $null
            if ([System.Version]::TryParse($versionValue, [ref]$parsedVersion)) {
                $buildNumber = $parsedVersion.Build
            }
        }

        if ($Description -match '(?i)Windows 11' -and $isX64 -and $buildNumber -ge 22000) {
            return 'Windows11X64'
        }
        if ($Description -match '(?i)Windows 10' -and $isX64 -and $buildNumber -ge 0) {
            return 'Windows10X64'
        }
    }
    elseif ($Family -eq 'Linux' `
        -and $Description -match '(?i)Ubuntu 24[.]04' `
        -and $Version -match '^24[.]04(?:[.]|$)' `
        -and $isX64) {
        return 'Ubuntu2404X64'
    }

    return 'Other'
}

function Get-HostEvidence {
    $description = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
    $architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
    $family = 'Other'
    $version = [Environment]::OSVersion.Version.ToString()

    if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::Windows)) {
        $family = 'Windows'
        try {
            $operatingSystem = Get-CimInstance -ClassName Win32_OperatingSystem -ErrorAction Stop
            $description = $operatingSystem.Caption
            $version = $operatingSystem.Version
            if ($operatingSystem.BuildNumber) {
                $version = "$version (build $($operatingSystem.BuildNumber))"
            }
        }
        catch {
            # RuntimeInformation remains a useful fallback when CIM is unavailable.
        }

    }
    elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::Linux)) {
        $family = 'Linux'
        if (Test-Path -LiteralPath '/etc/os-release') {
            $osRelease = Get-Content -LiteralPath '/etc/os-release' -Raw
            $prettyNameMatch = [regex]::Match($osRelease, '(?m)^PRETTY_NAME="?(?<value>[^"\r\n]+)"?$')
            $versionMatch = [regex]::Match($osRelease, '(?m)^VERSION_ID="?(?<value>[^"\r\n]+)"?$')
            if ($prettyNameMatch.Success) {
                $description = $prettyNameMatch.Groups['value'].Value
            }
            if ($versionMatch.Success) {
                $version = $versionMatch.Groups['value'].Value
            }
        }

    }

    $classification = Get-HostClassification `
        -Family $family `
        -Description $description `
        -Version $version `
        -Architecture $architecture

    return [ordered]@{
        family = $family
        description = $description
        version = $version
        architecture = $architecture
        classification = $classification
    }
}

function Assert-SafeHeaderValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value) -or $Value.Contains('|') -or $Value.Contains("`r") -or $Value.Contains("`n")) {
        throw "$Name must be a non-empty single-line value without a pipe character."
    }
}

$resolvedArtifact = (Resolve-Path -LiteralPath $ArtifactPath).Path
if (-not (Test-Path -LiteralPath $resolvedArtifact -PathType Leaf)) {
    throw "Artifact was not found: $ArtifactPath"
}

Assert-SafeHeaderValue -Name 'Reviewer' -Value $Reviewer
if ([string]::IsNullOrWhiteSpace($SessionLabel)) {
    $SessionLabel = 'live-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
}

$release = Read-ReleaseMetadata -Path $resolvedArtifact
foreach ($property in @('version', 'commit', 'sourceUrl', 'license')) {
    if ($null -eq $release.PSObject.Properties[$property] -or [string]::IsNullOrWhiteSpace([string]$release.$property)) {
        throw "release-metadata.json is missing required property '$property'."
    }
}

$commit = ([string]$release.commit).ToLowerInvariant()
if ($commit -notmatch '^[0-9a-f]{40}$') {
    throw 'release-metadata.json must contain a full 40-character Git commit SHA.'
}
if ($ExpectedCommit -and $commit -ne $ExpectedCommit.ToLowerInvariant()) {
    throw "Artifact commit $commit does not match expected commit $($ExpectedCommit.ToLowerInvariant())."
}
if ([string]$release.license -ne 'AGPL-3.0') {
    throw "Artifact license must be AGPL-3.0, found '$($release.license)'."
}

$hostEvidence = Get-HostEvidence
if ($RequiredHost -ne 'Any' -and $hostEvidence.classification -ne $RequiredHost) {
    throw "This run requires host $RequiredHost, but the detected host is $($hostEvidence.classification): $($hostEvidence.description) $($hostEvidence.version) $($hostEvidence.architecture)."
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot '.tmp\soulseek-live-checklists'
}
elseif (-not [IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot $OutputRoot
}

$timestamp = [DateTimeOffset]::Now
$artifactName = [IO.Path]::GetFileName($resolvedArtifact)
$artifactStem = $artifactName -replace '(?i)\.tar\.gz$|\.zip$', ''
$safeStem = $artifactStem -replace '[^A-Za-z0-9._-]', '-'
$runName = '{0}-{1}-{2}' -f $timestamp.ToString('yyyyMMdd-HHmmss'), $commit.Substring(0, 7), $safeStem
$runDirectory = Join-Path ([IO.Path]::GetFullPath($OutputRoot)) $runName
if (Test-Path -LiteralPath $runDirectory) {
    throw "Run directory already exists: $runDirectory"
}

New-Item -ItemType Directory -Path $runDirectory | Out-Null
New-Item -ItemType Directory -Path (Join-Path $runDirectory 'evidence') | Out-Null

$artifactHash = (Get-FileHash -LiteralPath $resolvedArtifact -Algorithm SHA256).Hash.ToLowerInvariant()
$metadata = [ordered]@{
    schemaVersion = 1
    createdAtUtc = $timestamp.ToUniversalTime().ToString('o')
    createdAtLocal = $timestamp.ToString('o')
    artifactName = $artifactName
    artifactSha256 = $artifactHash
    artifactVersion = [string]$release.version
    commit = $commit
    sourceUrl = [string]$release.sourceUrl
    license = [string]$release.license
    reviewer = $Reviewer
    sessionLabel = $SessionLabel
    networkMode = $NetworkMode
    requiredHost = $RequiredHost
    host = $hostEvidence
}

$metadataPath = Join-Path $runDirectory 'run-metadata.json'
$metadata | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $metadataPath -Encoding UTF8

$date = $timestamp.ToString('yyyy-MM-dd')
$checklist = @"
<!-- sockseek-live-checklist:v1 -->
# Soulseek live checklist evidence

## Run header

| Field | Value |
| --- | --- |
| Date | $date |
| Commit | $commit |
| Artifact | $artifactName; version $($release.version); SHA256 $artifactHash |
| Reviewer | $Reviewer |
| Soulseek account/session | $SessionLabel (redacted local label) |
| Network mode | $NetworkMode |
| Evidence location | checklist.md, run-metadata.json and relative files under evidence/ |
| Host | $($hostEvidence.description); $($hostEvidence.version); $($hostEvidence.architecture); $($hostEvidence.classification) |

## Safety

- Never record the Soulseek username or password, provider tokens, authorization headers, private playlist URLs or chat message text.
- Use only relative evidence paths below evidence/; redact screenshots before placing them there.
- Use a dedicated test account and a consenting second test participant for peer-facing upload, chat and browse checks.
- Record each observation time in UTC as yyyy-MM-ddTHH:mm:ssZ.

## Feature evidence

Replace every placeholder in this file. A Soulseek-enabled run requires Pass for every row.

| Area | Status | UTC time | Redacted evidence |
| --- | --- | --- | --- |
| Search | Pending | TODO | TODO |
| Wishlist | Pending | TODO | TODO |
| Download | Pending | TODO | TODO |
| Upload / sharing | Pending | TODO | TODO |
| Chat | Pending | TODO | TODO |
| Privileges | Pending | TODO | TODO |
| Folder browse | Pending | TODO | TODO |
| Sharing counts/status | Pending | TODO | TODO |
| Legal-use messaging | Pending | TODO | TODO |
| Provider audio invariant | Pending | TODO | TODO |

## Run notes

- Start time UTC: TODO
- End time UTC: TODO
- Diagnostics export reviewed and redacted: TODO
- Automated gate transcript or exact reference: TODO
- Failures, retries or deviations: TODO

This record is engineering evidence, not legal advice, and does not by itself approve a public beta. ADR-0009 remains binding until an accepted decision supersedes it.
"@

$checklistPath = Join-Path $runDirectory 'checklist.md'
Set-Content -LiteralPath $checklistPath -Value $checklist -Encoding UTF8

Write-Host 'Soulseek live checklist scaffold created.'
Write-Host "Run directory: $runDirectory"
Write-Host "Artifact commit: $commit"
Write-Host "Artifact SHA256: $artifactHash"
Write-Host "Detected host: $($hostEvidence.classification)"
Write-Host 'No credentials or account name were collected.'

