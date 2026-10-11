param(
    [string]$AuditPath = 'docs/soulseek-compliance-audit.md',
    [string]$AdrPath = 'docs/adr/0009-closed-beta-until-soulseek-compliance.md',
    [string]$LimitationsPath = 'docs/beta-limitations.md'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Resolve-RepoPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path $repoRoot $Path
}

function Read-RequiredText {
    param(
        [string]$Path,
        [string]$Description
    )

    $resolved = Resolve-RepoPath $Path
    if (-not (Test-Path -LiteralPath $resolved)) {
        throw "$Description was not found: $resolved"
    }

    return Get-Content -LiteralPath $resolved -Raw
}

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Needle,
        [string]$Description
    )

    if ($Text.IndexOf($Needle, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "Missing ${Description}: '$Needle'"
    }
}

function Assert-AuditRow {
    param(
        [string]$Audit,
        [string]$Area,
        [string]$StatusNeedle,
        [string]$RiskNeedle
    )

    $pattern = "(?im)^\|\s*$([regex]::Escape($Area))\s*\|\s*(?<status>.*?)\s*\|\s*(?<risk>.*?)\s*\|$"
    $match = [regex]::Match($Audit, $pattern)
    if (-not $match.Success) {
        throw "Missing Soulseek compliance audit row: $Area"
    }

    Assert-Contains $match.Groups['status'].Value $StatusNeedle "$Area status"
    Assert-Contains $match.Groups['risk'].Value $RiskNeedle "$Area public beta risk"
}

$audit = Read-RequiredText -Path $AuditPath -Description 'Soulseek compliance audit'
$adr = Read-RequiredText -Path $AdrPath -Description 'ADR-0009'
$limitations = Read-RequiredText -Path $LimitationsPath -Description 'Beta limitations document'

Assert-Contains $audit 'https://www.slsknet.org/news/node/681' 'Soulseek rules source link'
Assert-Contains $audit 'https://www.slsknet.org/news/node/682' 'Soulseek terms source link'
Assert-Contains $audit 'Sockseek is not ready for public beta distribution with Soulseek connectivity enabled' 'audit public beta blocker'
Assert-Contains $audit 'closed-beta-until-soulseek-compliance' 'audit ADR reference'
Assert-Contains $audit 'implements and verifies the missing Soulseek feature coverage' 'audit implementation resolution option'
Assert-Contains $audit 'does not connect to the Soulseek network' 'audit non-connecting distribution option'
Assert-Contains $audit 'explicit compliance/legal decision' 'audit compliance/legal decision option'

Assert-AuditRow -Audit $audit -Area 'Wishlist' -StatusNeedle 'Not implemented' -RiskNeedle 'High'
Assert-AuditRow -Audit $audit -Area 'Upload / sharing' -StatusNeedle 'Not implemented or verified' -RiskNeedle 'High'
Assert-AuditRow -Audit $audit -Area 'Chat' -StatusNeedle 'Not implemented' -RiskNeedle 'High'
Assert-AuditRow -Audit $audit -Area 'Privileges' -StatusNeedle 'Not implemented or verified' -RiskNeedle 'High'

Assert-Contains $adr '## Status' 'ADR status section'
Assert-Contains $adr 'Accepted' 'ADR accepted status'
Assert-Contains $adr 'Do not publish a public beta with Soulseek network connectivity enabled' 'ADR public beta prohibition'
Assert-Contains $adr 'closed/internal beta and release-candidate testing only' 'ADR closed/internal-only allowance'
Assert-Contains $adr 'implements and verifies the missing Soulseek feature coverage' 'ADR implementation resolution option'
Assert-Contains $adr 'disables Soulseek network connectivity for the public distribution mode' 'ADR non-connecting distribution option'
Assert-Contains $adr 'explicit legal/product decision' 'ADR legal/product decision option'
Assert-Contains $adr 'No provider audio, external-service playback or provider downloading is introduced' 'ADR provider audio invariant'

Assert-Contains $limitations 'Public beta: blocked by ADR-0009' 'limitations public beta blocker'
Assert-Contains $limitations 'Closed/internal beta: allowed' 'limitations closed/internal beta allowance'
Assert-Contains $limitations 'External services are playlist or metadata sources only' 'limitations provider metadata-only policy'
Assert-Contains $limitations 'Spotify, YouTube, Bandcamp and MusicBrainz are never audio sources' 'limitations provider no-audio policy'
Assert-Contains $limitations 'Public beta is blocked until compliance coverage for wishlist, upload/sharing, chat and privilege recognition is resolved' 'limitations Soulseek unresolved features'

Write-Host 'Soulseek compliance gate validation passed.'
