param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactPath,

    [Parameter(Mandatory = $true)]
    [string]$RunDirectory
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

function Assert-Contains {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,

        [Parameter(Mandatory = $true)]
        [string]$Expected,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    if ($Text.IndexOf($Expected, [StringComparison]::Ordinal) -lt 0) {
        throw "Checklist is missing $Description."
    }
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

$resolvedArtifact = (Resolve-Path -LiteralPath $ArtifactPath).Path
$resolvedRunDirectory = (Resolve-Path -LiteralPath $RunDirectory).Path
$metadataPath = Join-Path $resolvedRunDirectory 'run-metadata.json'
$checklistPath = Join-Path $resolvedRunDirectory 'checklist.md'
if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) {
    throw "Run metadata was not found: $metadataPath"
}
if (-not (Test-Path -LiteralPath $checklistPath -PathType Leaf)) {
    throw "Checklist was not found: $checklistPath"
}

$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
if ($metadata.schemaVersion -ne 1) {
    throw "Unsupported live checklist schema version: $($metadata.schemaVersion)"
}

$release = Read-ReleaseMetadata -Path $resolvedArtifact
$actualHash = (Get-FileHash -LiteralPath $resolvedArtifact -Algorithm SHA256).Hash.ToLowerInvariant()
$artifactName = [IO.Path]::GetFileName($resolvedArtifact)
$expectedCommit = ([string]$release.commit).ToLowerInvariant()

if ([string]$metadata.artifactName -ne $artifactName) {
    throw "Run metadata names artifact '$($metadata.artifactName)', but '$artifactName' was supplied."
}
if ([string]$metadata.artifactSha256 -ne $actualHash) {
    throw 'Artifact SHA256 does not match the hash captured when the run was created.'
}
if ([string]$metadata.commit -ne $expectedCommit) {
    throw 'Run commit does not match release-metadata.json in the supplied artifact.'
}
if ([string]$metadata.artifactVersion -ne [string]$release.version) {
    throw 'Run version does not match release-metadata.json in the supplied artifact.'
}
if ([string]$metadata.license -ne 'AGPL-3.0' -or [string]$release.license -ne 'AGPL-3.0') {
    throw 'Run and artifact must retain the AGPL-3.0 license.'
}
$allowedRequiredHosts = @('Any', 'Windows11X64', 'Ubuntu2404X64')
if ($allowedRequiredHosts -notcontains [string]$metadata.requiredHost) {
    throw "Run metadata contains an unsupported required host: $($metadata.requiredHost)"
}
$hostClassification = Get-HostClassification `
    -Family ([string]$metadata.host.family) `
    -Description ([string]$metadata.host.description) `
    -Version ([string]$metadata.host.version) `
    -Architecture ([string]$metadata.host.architecture)
if ([string]$metadata.host.classification -ne $hostClassification) {
    throw "Captured host classification '$($metadata.host.classification)' does not match recomputed classification '$hostClassification'."
}
if ([string]$metadata.requiredHost -ne 'Any' -and $hostClassification -ne [string]$metadata.requiredHost) {
    throw 'Captured host classification does not satisfy the run requirement.'
}

$checklist = Get-Content -LiteralPath $checklistPath -Raw
Assert-Contains -Text $checklist -Expected '<!-- sockseek-live-checklist:v1 -->' -Description 'the live checklist schema marker'
Assert-Contains -Text $checklist -Expected $expectedCommit -Description 'the exact artifact commit'
Assert-Contains -Text $checklist -Expected $actualHash -Description 'the exact artifact SHA256'
Assert-Contains -Text $checklist -Expected ([string]$metadata.sessionLabel) -Description 'the redacted session label'
Assert-Contains -Text $checklist -Expected ([string]$metadata.networkMode) -Description 'the network mode'
Assert-Contains -Text $checklist -Expected ([string]$metadata.host.description) -Description 'the captured host description'
Assert-Contains -Text $checklist -Expected ([string]$metadata.host.version) -Description 'the captured host version'
Assert-Contains -Text $checklist -Expected ([string]$metadata.host.architecture) -Description 'the captured host architecture'
Assert-Contains -Text $checklist -Expected $hostClassification -Description 'the recomputed host classification'

$redactionText = $checklist + [Environment]::NewLine + (Get-Content -LiteralPath $metadataPath -Raw)
$evidenceDirectory = Join-Path $resolvedRunDirectory 'evidence'
if (Test-Path -LiteralPath $evidenceDirectory -PathType Container) {
    $textExtensions = @('.txt', '.md', '.json', '.log', '.csv', '.xml', '.yaml', '.yml')
    foreach ($evidenceFile in Get-ChildItem -LiteralPath $evidenceDirectory -File -Recurse) {
        if ($textExtensions -contains $evidenceFile.Extension.ToLowerInvariant() -and $evidenceFile.Length -le 5MB) {
            $redactionText += [Environment]::NewLine + (Get-Content -LiteralPath $evidenceFile.FullName -Raw)
        }
    }
}

$sensitivePatterns = [ordered]@{
    'a password assignment' = '(?im)["'']?[A-Za-z0-9_-]*password["'']?\s*[:=]'
    'a password command-line option' = '(?im)(?:^|\s)--?password(?:\s+|=)'
    'an Authorization credential' = '(?im)["'']?authorization["'']?\s*[:=]\s*["'']?(?:bearer|basic|digest|negotiate)\s+\S+'
    'a token or secret assignment' = '(?im)["'']?(?:[A-Za-z0-9_-]*(?:token|secret)|code[_-]?verifier|oauth[_-]?code)["'']?\s*[:=]'
    'an OAuth code JSON property' = '(?im)["'']code["'']\s*:'
    'a sensitive URL query parameter' = '(?im)https?://\S+[?&](token|access_token|refresh_token|code|key|secret)='
}
foreach ($entry in $sensitivePatterns.GetEnumerator()) {
    if ([regex]::IsMatch($redactionText, $entry.Value)) {
        throw "The run's text evidence appears to contain $($entry.Key); redact it before validation."
    }
}

$areas = @(
    'Search',
    'Wishlist',
    'Download',
    'Upload / sharing',
    'Chat',
    'Privileges',
    'Folder browse',
    'Sharing counts/status',
    'Legal-use messaging',
    'Provider audio invariant'
)
$lines = $checklist -split "`r?`n"
foreach ($area in $areas) {
    $escapedArea = [regex]::Escape($area)
    $matches = @($lines | Where-Object { $_ -match "^\|\s*$escapedArea\s*\|" })
    if ($matches.Count -ne 1) {
        throw "Expected exactly one checklist row for '$area', found $($matches.Count)."
    }

    $cells = @($matches[0].Trim().Trim('|').Split('|') | ForEach-Object { $_.Trim() })
    if ($cells.Count -ne 4) {
        throw "Checklist row '$area' must have exactly four columns and evidence must not contain a pipe character."
    }

    $status = $cells[1]
    if ($status -eq 'Fail') {
        throw "Checklist row '$area' is marked Fail; public beta remains blocked."
    }
    if ($status -eq 'Not applicable by accepted ADR') {
        if ([string]$metadata.networkMode -ne 'Soulseek disabled for public distribution' -or $cells[3] -notmatch '(?i)ADR-[0-9]{4}') {
            throw "Checklist row '$area' may be not applicable only for a disabled-network artifact with an accepted ADR reference."
        }
    }
    elseif ($status -ne 'Pass') {
        throw "Checklist row '$area' must be Pass, Fail or Not applicable by accepted ADR; found '$status'."
    }

    $observedAt = [DateTimeOffset]::MinValue
    $parsedTimestamp = [DateTimeOffset]::TryParseExact(
        $cells[2],
        'yyyy-MM-ddTHH:mm:ssZ',
        [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::AssumeUniversal,
        [ref]$observedAt)
    if (-not $parsedTimestamp) {
        throw "Checklist row '$area' must contain a UTC timestamp formatted as yyyy-MM-ddTHH:mm:ssZ."
    }
    if ([string]::IsNullOrWhiteSpace($cells[3]) -or $cells[3] -eq 'TODO') {
        throw "Checklist row '$area' must contain redacted evidence."
    }
}

if ($checklist -match '(?i)\bTODO\b') {
    throw 'Checklist still contains TODO placeholders.'
}

Write-Host 'Soulseek live checklist validation passed.'
Write-Host "Commit: $expectedCommit"
Write-Host "Artifact SHA256: $actualHash"
Write-Host "Host: $($metadata.host.classification)"

