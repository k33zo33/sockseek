param(
    [string]$Commit = '',

    [string]$SourceUrl = 'https://github.com/k33zo33/sockseek',

    [string]$OutputPath = '',

    [string]$ArtifactName = 'Sockseek closed/internal beta build',

    [string]$Version = 'dev'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-Commit {
    param([string]$RequestedCommit)

    if (-not [string]::IsNullOrWhiteSpace($RequestedCommit)) {
        return $RequestedCommit.Trim()
    }

    $commit = (& git rev-parse --short HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($commit)) {
        throw 'Unable to resolve the current Git commit. Pass -Commit explicitly.'
    }

    return $commit
}

function Read-RequiredFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Required document not found: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw
}

$Commit = Resolve-Commit $Commit

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = "artifacts/closed-beta-tester-instructions-$Commit.md"
}

$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

$betaLimitations = Read-RequiredFile 'docs/beta-limitations.md'
$diagnostics = Read-RequiredFile 'docs/diagnostics-feedback.md'
$security = Read-RequiredFile 'SECURITY.md'

$legalUseSection = [Regex]::Match(
    $betaLimitations,
    '(?s)## Legal-use notice\s*(.*?)\s*## Provider limitations')
if (-not $legalUseSection.Success) {
    throw "docs/beta-limitations.md is missing the Legal-use notice section."
}

$providerLimitationsSection = [Regex]::Match(
    $betaLimitations,
    '(?s)## Provider limitations\s*(.*?)\s*## Operational limitations')
if (-not $providerLimitationsSection.Success) {
    throw "docs/beta-limitations.md is missing the Provider limitations section."
}

$operationalLimitationsSection = [Regex]::Match(
    $betaLimitations,
    '(?s)## Operational limitations\s*(.*?)\s*## Diagnostics for closed beta testers')
if (-not $operationalLimitationsSection.Success) {
    throw "docs/beta-limitations.md is missing the Operational limitations section."
}

$diagnosticsIncludeSection = [Regex]::Match(
    $diagnostics,
    '(?s)## What testers should include\s*(.*?)\s*## Desktop diagnostics')
if (-not $diagnosticsIncludeSection.Success) {
    throw "docs/diagnostics-feedback.md is missing the tester diagnostics section."
}

$diagnosticsDoNotSendSection = [Regex]::Match(
    $diagnostics,
    '(?s)## Do not include\s*(.*?)\s*## Routing')
if (-not $diagnosticsDoNotSendSection.Success) {
    throw "docs/diagnostics-feedback.md is missing the redaction section."
}

$securityPrivateReportLine = if ($security -match '(?m)^Sockseek is a local-first.*$') {
    $Matches[0]
}
else {
    'Report suspected vulnerabilities privately through SECURITY.md.'
}

$instructions = @"
# Sockseek closed/internal beta tester instructions

Generated on $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz').

## Build identity

| Field | Value |
| --- | --- |
| Artifact | $ArtifactName |
| Version | $Version |
| Commit | $Commit |
| Source | $SourceUrl |
| License | AGPL-3.0 |

This build is for closed/internal testing only. Do not present it as a public beta or stable release.

## Release status

- Public beta remains blocked until docs/beta-go-no-go.md says public beta is GO for this exact commit or tag.
- Soulseek compliance is unresolved under ADR-0009; do not distribute this build publicly with Soulseek network connectivity enabled.
- Include LICENSE, THIRD-PARTY-NOTICES, docs/beta-limitations.md and SECURITY.md with the tester package or tester instructions.

## Legal-use notice

$($legalUseSection.Groups[1].Value.Trim())

## Provider and operational limitations

$($providerLimitationsSection.Groups[1].Value.Trim())

$($operationalLimitationsSection.Groups[1].Value.Trim())

## Diagnostics

Ask testers to include:

$($diagnosticsIncludeSection.Groups[1].Value.Trim())

Do not ask testers to send:

$($diagnosticsDoNotSendSection.Groups[1].Value.Trim())

Security reports:

$securityPrivateReportLine

## Tester reminder

- Identify the exact commit $Commit in every bug report.
- Use the app Copy diagnostics action when available.
- Never paste provider tokens, OAuth codes, PKCE code verifiers, client secrets, Soulseek passwords, full Authorization headers or private playlist URLs with sensitive query parameters.
"@

Set-Content -Path $OutputPath -Value $instructions -Encoding UTF8
Write-Host "Wrote closed beta tester instructions to $OutputPath"
