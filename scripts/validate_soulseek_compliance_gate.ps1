param(
    [string]$AuditPath = 'docs/soulseek-compliance-audit.md',
    [string]$AdrPath = 'docs/adr/0009-closed-beta-until-soulseek-compliance.md',
    [string]$LimitationsPath = 'docs/beta-limitations.md',
    [string]$ChecklistPath = 'docs/soulseek-public-beta-compliance-checklist.md'
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
$checklist = Read-RequiredText -Path $ChecklistPath -Description 'Soulseek public beta compliance checklist'

Assert-Contains $audit 'https://www.slsknet.org/news/node/681' 'Soulseek rules source link'
Assert-Contains $audit 'https://www.slsknet.org/news/node/682' 'Soulseek terms source link'
Assert-Contains $audit 'Sockseek is not ready for public beta distribution with Soulseek connectivity enabled' 'audit public beta blocker'
Assert-Contains $audit 'closed-beta-until-soulseek-compliance' 'audit ADR reference'
Assert-Contains $audit 'docs/soulseek-public-beta-compliance-checklist.md' 'audit public beta checklist reference'
Assert-Contains $audit 'implements and verifies the missing Soulseek feature coverage' 'audit implementation resolution option'
Assert-Contains $audit 'does not connect to the Soulseek network' 'audit non-connecting distribution option'
Assert-Contains $audit 'explicit compliance/legal decision' 'audit compliance/legal decision option'

Assert-AuditRow -Audit $audit -Area 'Wishlist' -StatusNeedle 'Implemented across persisted daemon scheduling' -RiskNeedle 'live-network smoke'
Assert-AuditRow -Audit $audit -Area 'Upload / sharing' -StatusNeedle 'Explicit persisted roots' -RiskNeedle 'live verification'
Assert-AuditRow -Audit $audit -Area 'Chat' -StatusNeedle 'bounded RAM-only state' -RiskNeedle 'live-network smoke'
Assert-AuditRow -Audit $audit -Area 'Privileges' -StatusNeedle 'recognition plus explicit acknowledgement' -RiskNeedle 'live-network smoke'
Assert-Contains $audit 'Run redacted live verification for wishlist, chat, upload and privilege recognition on the exact release candidate' 'audit exact-candidate live verification requirement'

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
Assert-Contains $limitations 'Public beta remains blocked by ADR-0009 until redacted live Soulseek verification passes' 'limitations Soulseek live verification blocker'

Assert-Contains $checklist 'not legal advice' 'checklist legal disclaimer'
Assert-Contains $checklist 'does not by itself approve a public beta' 'checklist no approval disclaimer'
Assert-Contains $checklist 'Commit' 'checklist commit evidence field'
Assert-Contains $checklist 'Soulseek account/session' 'checklist redacted account evidence field'
Assert-Contains $checklist 'Network mode' 'checklist network mode evidence field'
Assert-Contains $checklist 'Search' 'checklist search feature row'
Assert-Contains $checklist 'Wishlist' 'checklist wishlist feature row'
Assert-Contains $checklist 'Download' 'checklist download feature row'
Assert-Contains $checklist 'Upload / sharing' 'checklist upload sharing feature row'
Assert-Contains $checklist 'Chat' 'checklist chat feature row'
Assert-Contains $checklist 'Privileges' 'checklist privileges feature row'
Assert-Contains $checklist 'Folder browse' 'checklist folder browse feature row'
Assert-Contains $checklist 'Sharing counts/status' 'checklist sharing status feature row'
Assert-Contains $checklist 'Legal-use messaging' 'checklist legal-use messaging row'
Assert-Contains $checklist 'Provider audio invariant' 'checklist provider audio invariant row'
Assert-Contains $checklist 'Do not publish a public beta while ADR-0009 remains the active decision' 'checklist ADR-0009 stop condition'
Assert-Contains $checklist 'wishlist, upload/sharing, chat or privilege recognition is missing or unverified' 'checklist missing feature stop condition'
Assert-Contains $checklist 'provider tokens, OAuth codes, PKCE code verifiers, client secrets, Soulseek passwords, full Authorization headers or private playlist URLs' 'checklist diagnostics redaction stop condition'

Write-Host 'Soulseek compliance gate validation passed.'
