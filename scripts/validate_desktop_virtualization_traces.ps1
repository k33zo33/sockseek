param(
    [Parameter(Mandatory = $true)]
    [string]$LibraryReportPath,

    [Parameter(Mandatory = $true)]
    [string]$SearchReportPath,

    [Parameter(Mandatory = $true)]
    [string]$PlaylistReportPath,

    [ValidateRange(1, 1000000)]
    [int]$MinimumLibraryFixtureSize = 100000,

    [ValidateRange(1, 1000000)]
    [int]$MinimumSearchFixtureSize = 10000,

    [ValidateRange(1, 1000000)]
    [int]$MinimumPlaylistFixtureSize = 10000
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-TraceField {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Content,

        [Parameter(Mandatory = $true)]
        [string]$Field
    )

    $escapedField = [Regex]::Escape($Field)
    $match = [Regex]::Match($Content, "(?m)^\|\s*$escapedField\s*\|\s*(.*?)\s*\|$")
    if (-not $match.Success) {
        throw "Trace report is missing metadata field '$Field'."
    }

    return $match.Groups[1].Value.Trim().Trim('`')
}

function Read-TraceDefects {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Content
    )

    $match = [Regex]::Match($Content, "(?s)## Observed UI defects\s*(.*?)\s*## Gate note")
    if (-not $match.Success) {
        throw "Trace report is missing the 'Observed UI defects' section."
    }

    $defects = $match.Groups[1].Value.Trim()
    if ([string]::IsNullOrWhiteSpace($defects)) {
        throw "Trace report has an empty 'Observed UI defects' section."
    }

    return $defects
}

function Read-TraceBoolean {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Content,

        [Parameter(Mandatory = $true)]
        [string]$Field
    )

    $valueText = Read-TraceField -Content $Content -Field $Field
    $value = $false
    if (-not [bool]::TryParse($valueText, [ref]$value)) {
        throw "Trace report has invalid boolean value for '$Field': '$valueText'."
    }

    return $value
}

function Test-TraceReport {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedSurface,

        [Parameter(Mandatory = $true)]
        [int]$MinimumFixtureSize
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $fullPath)) {
        throw "Trace report not found: $fullPath"
    }

    $content = Get-Content -LiteralPath $fullPath -Raw
    $surface = Read-TraceField -Content $content -Field 'Surface'
    if ($surface -ne $ExpectedSurface) {
        throw "Trace report '$fullPath' has surface '$surface'; expected '$ExpectedSurface'."
    }

    $fixtureSizeText = Read-TraceField -Content $content -Field 'Fixture size'
    $fixtureSize = 0
    if (-not [int]::TryParse($fixtureSizeText, [ref]$fixtureSize)) {
        throw "Trace report '$fullPath' has invalid fixture size '$fixtureSizeText'."
    }

    if ($fixtureSize -lt $MinimumFixtureSize) {
        throw "Trace report '$fullPath' fixture size $fixtureSize is below required minimum $MinimumFixtureSize."
    }

    $commit = Read-TraceField -Content $content -Field 'Commit'
    if ([string]::IsNullOrWhiteSpace($commit) -or $commit -eq 'unknown') {
        throw "Trace report '$fullPath' does not identify a concrete commit."
    }

    $hasMainWindow = Read-TraceBoolean -Content $content -Field 'Main window present'
    if (-not $hasMainWindow) {
        throw "Trace report '$fullPath' was not captured from a rendered main window."
    }

    $allowHeadlessProcess = Read-TraceBoolean -Content $content -Field 'Headless process allowed'
    if ($allowHeadlessProcess) {
        throw "Trace report '$fullPath' was captured with -AllowHeadlessProcess and cannot satisfy the rendered UI gate."
    }

    $rawSamples = Read-TraceField -Content $content -Field 'Raw samples'
    $rawSamplesPath = if ([System.IO.Path]::IsPathRooted($rawSamples)) {
        $rawSamples
    }
    elseif (Test-Path -LiteralPath ([System.IO.Path]::GetFullPath($rawSamples))) {
        [System.IO.Path]::GetFullPath($rawSamples)
    }
    else {
        Join-Path (Split-Path -Parent $fullPath) $rawSamples
    }

    if (-not (Test-Path -LiteralPath $rawSamplesPath)) {
        throw "Trace report '$fullPath' references missing raw samples: $rawSamples"
    }

    $defects = Read-TraceDefects -Content $content
    [pscustomobject]@{
        Surface = $surface
        FixtureSize = $fixtureSize
        Commit = $commit
        Report = $fullPath
        RawSamples = [System.IO.Path]::GetFullPath($rawSamplesPath)
        Defects = $defects
    }
}

$results = @(
    Test-TraceReport -Path $LibraryReportPath -ExpectedSurface 'library' -MinimumFixtureSize $MinimumLibraryFixtureSize
    Test-TraceReport -Path $SearchReportPath -ExpectedSurface 'search' -MinimumFixtureSize $MinimumSearchFixtureSize
    Test-TraceReport -Path $PlaylistReportPath -ExpectedSurface 'playlist' -MinimumFixtureSize $MinimumPlaylistFixtureSize
)

$commits = @($results | Select-Object -ExpandProperty Commit -Unique)
if ($commits.Count -ne 1) {
    throw "Trace reports do not all use the same commit: $($commits -join ', ')"
}

Write-Host "Desktop virtualization trace validation passed for commit $($commits[0])."
foreach ($result in $results) {
    Write-Host "$($result.Surface): fixture=$($result.FixtureSize); report=$($result.Report)"
}
