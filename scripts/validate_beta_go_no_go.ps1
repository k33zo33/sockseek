param(
    [string]$GoNoGoPath = 'docs/beta-go-no-go.md'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$resolvedPath = if ([System.IO.Path]::IsPathRooted($GoNoGoPath)) {
    $GoNoGoPath
} else {
    Join-Path $repoRoot $GoNoGoPath
}

if (-not (Test-Path -LiteralPath $resolvedPath)) {
    throw "Beta go/no-go document was not found: $resolvedPath"
}

$document = Get-Content -LiteralPath $resolvedPath -Raw

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

function Get-Section {
    param(
        [string]$Text,
        [string]$Heading
    )

    $pattern = "(?ms)^##\s+$([regex]::Escape($Heading))\s*(?<body>.*?)(?=^##\s+|\z)"
    $match = [regex]::Match($Text, $pattern)
    if (-not $match.Success) {
        throw "Missing section: $Heading"
    }

    return $match.Groups['body'].Value
}

function Assert-TableRowStatus {
    param(
        [string]$Text,
        [string]$FirstCell,
        [string]$ExpectedStatus
    )

    $pattern = "(?im)^\|\s*$([regex]::Escape($FirstCell))\s*\|.*\|\s*$([regex]::Escape($ExpectedStatus))\s*\|$"
    if (-not [regex]::IsMatch($Text, $pattern)) {
        throw "Expected table row '$FirstCell' to have status '$ExpectedStatus'."
    }
}

function Assert-TableRowContains {
    param(
        [string]$Text,
        [string]$FirstCell,
        [string]$Needle,
        [string]$Description
    )

    $pattern = "(?im)^\|\s*$([regex]::Escape($FirstCell))\s*\|(?<row>.*)\|.*\|$"
    $match = [regex]::Match($Text, $pattern)
    if (-not $match.Success) {
        throw "Missing table row: $FirstCell"
    }

    Assert-Contains $match.Groups['row'].Value $Needle $Description
}

$decisionPattern = '(?im)^\|\s*Public beta\s*\|\s*NO-GO\s*\|\s*(?<reason>.*?)\s*\|$'
$decisionMatch = [regex]::Match($document, $decisionPattern)
if (-not $decisionMatch.Success) {
    throw "Public beta decision must remain NO-GO until Sprint 15 blockers are resolved and this gate is intentionally updated."
}

$publicBetaReason = $decisionMatch.Groups['reason'].Value
Assert-Contains $publicBetaReason 'ADR-0009' 'public beta Soulseek compliance blocker'
Assert-Contains $publicBetaReason 'target-OS packaging evidence' 'public beta target OS packaging blocker'

Assert-TableRowStatus $document 'Soulseek compliance decision' 'Public beta blocked'
Assert-TableRowStatus $document 'Rendered UI virtualization trace' 'Passed for Windows RC'
Assert-TableRowStatus $document 'Docker/headless smoke' 'Passed'
Assert-TableRowStatus $document 'Release limitations' 'Passed'
Assert-TableRowStatus $document 'Security and crash reporting' 'Passed'
Assert-TableRowStatus $document 'Provider-audio scope guard' 'Passed'
Assert-TableRowStatus $document 'Windows package smoke' 'Passed for Windows RC'

Assert-TableRowContains $document 'Soulseek compliance decision' 'scripts/validate_soulseek_compliance_gate.ps1' 'Soulseek compliance gate validator evidence'
Assert-TableRowContains $document 'Soulseek compliance decision' 'scripts/test_soulseek_compliance_gate_validator.ps1' 'Soulseek compliance gate validator smoke evidence'
Assert-TableRowContains $document 'Soulseek compliance decision' 'docs/soulseek-public-beta-compliance-checklist.md' 'Soulseek public beta checklist evidence'
Assert-TableRowContains $document 'Soulseek compliance decision' 'search, wishlist, download, upload/sharing, chat, privileges, folder browse, sharing status, legal-use messaging and the provider audio invariant' 'Soulseek public beta checklist coverage evidence'
Assert-TableRowContains $document 'Soulseek compliance decision' 'wishlist/upload-sharing/chat/privilege gaps' 'Soulseek unresolved feature blocker evidence'
Assert-TableRowContains $document 'Soulseek compliance decision' 'checklist evidence' 'Soulseek checklist negative-case evidence'
Assert-TableRowContains $document 'Soulseek compliance decision' 'provider no-audio policy' 'Soulseek compliance provider no-audio invariant evidence'
Assert-TableRowContains $document 'Release limitations' 'scripts/test_closed_beta_tester_instructions.ps1' 'closed beta tester instructions smoke evidence'
Assert-TableRowContains $document 'Release limitations' 'provider no-audio language' 'release limitations provider no-audio evidence'
Assert-TableRowContains $document 'Release limitations' 'secret-redaction reminders' 'release limitations diagnostics redaction evidence'
Assert-TableRowContains $document 'Security and crash reporting' 'scripts/validate_diagnostics_feedback_gate.ps1' 'diagnostics feedback gate validator evidence'
Assert-TableRowContains $document 'Security and crash reporting' 'scripts/test_diagnostics_feedback_gate_validator.ps1' 'diagnostics feedback gate validator smoke evidence'
Assert-TableRowContains $document 'Security and crash reporting' 'private vulnerability routing' 'diagnostics private vulnerability routing evidence'
Assert-TableRowContains $document 'Security and crash reporting' 'Authorization headers' 'diagnostics authorization header redaction evidence'
Assert-TableRowContains $document 'Rendered UI virtualization trace' 'exact packaged Windows executable' 'rendered trace packaged executable evidence'
Assert-TableRowContains $document 'Rendered UI virtualization trace' 'commit `6da97fd`' 'rendered trace exact commit evidence'
Assert-TableRowContains $document 'Rendered UI virtualization trace' '100% scaling' 'rendered trace display scaling evidence'
Assert-TableRowContains $document 'Rendered UI virtualization trace' 'Exact-package fixture preflight independently confirmed 100,000 SQLite library rows, 10,000 SQLite playlist items and 10,000 mock Soulseek files' 'rendered trace fixture preflight evidence'
Assert-TableRowContains $document 'Rendered UI virtualization trace' '180 seconds' 'rendered trace duration evidence'
Assert-TableRowContains $document 'Rendered UI virtualization trace' '90 raw samples' 'rendered trace sample-count evidence'
Assert-TableRowContains $document 'Rendered UI virtualization trace' '`960x720`' 'rendered trace narrow viewport evidence'
Assert-TableRowContains $document 'Rendered UI virtualization trace' '`1400x900`' 'rendered trace wide viewport evidence'
Assert-TableRowContains $document 'Rendered UI virtualization trace' '`100000 tracks`' 'rendered trace library fixture evidence'
Assert-TableRowContains $document 'Rendered UI virtualization trace' '`Revision 10000`' 'rendered trace search fixture evidence'
Assert-TableRowContains $document 'Rendered UI virtualization trace' '`10000/10000 tracks`' 'rendered trace playlist fixture evidence'
Assert-TableRowContains $document 'Rendered UI virtualization trace' 'visible `Download` actions' 'rendered trace Search action evidence'
Assert-TableRowContains $document 'Rendered UI virtualization trace' 'matching row actions usable' 'rendered trace Playlist action evidence'
Assert-TableRowContains $document 'Rendered UI virtualization trace' 'No text overlap' 'rendered trace visual defect evidence'
Assert-TableRowContains $document 'Rendered UI virtualization trace' 'scripts/validate_desktop_virtualization_traces.ps1' 'rendered trace validator evidence'
Assert-TableRowContains $document 'Rendered UI virtualization trace' 'artifacts/sprint15-6da97fd/' 'rendered trace local artifact evidence'
Assert-TableRowContains $document 'Windows package smoke' 'closed-beta-tester-instructions.md' 'Windows package closed beta instructions evidence'
Assert-TableRowContains $document 'Windows package smoke' 'SECURITY.md' 'Windows package security notice manifest evidence'
Assert-TableRowContains $document 'Windows package smoke' 'THIRD-PARTY-NOTICES' 'Windows package third-party notices manifest evidence'
Assert-TableRowContains $document 'Windows package smoke' 'docs/beta-limitations.md' 'Windows package beta limitations manifest evidence'
Assert-TableRowContains $document 'Windows package smoke' 'release-metadata.json' 'Windows package release metadata evidence'
Assert-TableRowContains $document 'Windows package smoke' '132-package SBOM' 'Windows package SBOM evidence'
Assert-TableRowContains $document 'Windows package smoke' 'commit `6da97fd`' 'Windows package exact commit evidence'
Assert-TableRowContains $document 'Windows package smoke' 'd6f016a5bf2013d4b12b101d283e02e14431b10ac767e65c273ffcf11cb0df45' 'Windows package archive SHA256 evidence'
Assert-TableRowContains $document 'Docker/headless smoke' 'isolated-`DOCKER_CONFIG`' 'Docker isolated config diagnostic evidence'
Assert-TableRowContains $document 'Docker/headless smoke' 'docker-users` membership' 'Docker Windows group diagnostic evidence'
Assert-TableRowContains $document 'Docker/headless smoke' 'config path readability' 'Docker Windows config ACL diagnostic evidence'
Assert-TableRowContains $document 'Docker/headless smoke' 'Docker Desktop service state' 'Docker Windows service diagnostic evidence'
Assert-TableRowContains $document 'Docker/headless smoke' 'WSL distribution state' 'Docker Windows WSL diagnostic evidence'
Assert-TableRowContains $document 'Docker/headless smoke' 'npipe:////./pipe/dockerDesktopLinuxEngine' 'Docker Desktop pipe diagnostic evidence'
Assert-TableRowContains $document 'Docker/headless smoke' 'engine-pipe access or timeout failures' 'Docker pipe failure classification evidence'
Assert-TableRowContains $document 'Docker/headless smoke' 'scripts/test_docker_smoke_diagnostics.ps1' 'Docker diagnostics artifact smoke evidence'
Assert-TableRowContains $document 'Docker/headless smoke' 'repo working-directory handoff' 'Docker helper working-directory regression evidence'
Assert-TableRowContains $document 'Docker/headless smoke' 'full Alpine `vlc` runtime package' 'Docker VLC runtime evidence'
Assert-TableRowContains $document 'Docker/headless smoke' 'trimming disabled' 'Docker trimming-disabled publish evidence'
Assert-TableRowContains $document 'Docker/headless smoke' 'without trim-analysis warning output' 'Docker trim warning reduction evidence'
Assert-TableRowContains $document 'Docker/headless smoke' 'scripts/run_docker_smoke.ps1` passed end to end' 'Docker full smoke pass evidence'
Assert-TableRowContains $document 'Docker/headless smoke' 'Docker Desktop `4.94.0`' 'Docker Desktop version evidence'
Assert-TableRowContains $document 'Docker/headless smoke' 'Docker Server `29.8.2`' 'Docker Server version evidence'
Assert-TableRowContains $document 'Docker/headless smoke' 'F:\Docker\DockerDesktopWSL' 'Docker F drive data-root evidence'
Assert-TableRowContains $document 'Docker/headless smoke' 'daemon container `/health` returning `{"status":"ok"}`' 'Docker daemon health evidence'

$requiredBeforePublicBeta = Get-Section $document 'Required before public beta'
Assert-Contains $requiredBeforePublicBeta 'Resolve ADR-0009' 'required public beta Soulseek compliance item'
Assert-Contains $requiredBeforePublicBeta 'target OS package smoke' 'required target OS package smoke item'
Assert-Contains $requiredBeforePublicBeta 'release notes' 'required release notes item'
Assert-Contains $requiredBeforePublicBeta 'source availability' 'required source availability item'

$closedBetaRequirements = Get-Section $document 'Closed beta requirements'
Assert-Contains $closedBetaRequirements 'internal or allowlisted testers' 'closed beta audience restriction'
Assert-Contains $closedBetaRequirements 'docs/beta-limitations.md' 'closed beta limitations notice'
Assert-Contains $closedBetaRequirements 'SECURITY.md' 'closed beta security notice'
Assert-Contains $closedBetaRequirements 'LICENSE' 'closed beta license notice'
Assert-Contains $closedBetaRequirements 'THIRD-PARTY-NOTICES' 'closed beta third-party notices'
Assert-Contains $closedBetaRequirements 'scripts/write_closed_beta_tester_instructions.ps1' 'closed beta instruction generator'
Assert-Contains $closedBetaRequirements 'Copy diagnostics' 'closed beta diagnostics guidance'
Assert-Contains $closedBetaRequirements 'tokens' 'closed beta token redaction warning'
Assert-Contains $closedBetaRequirements 'OAuth codes' 'closed beta OAuth redaction warning'
Assert-Contains $closedBetaRequirements 'client secrets' 'closed beta client secret redaction warning'
Assert-Contains $closedBetaRequirements 'Soulseek passwords' 'closed beta Soulseek password redaction warning'
Assert-Contains $closedBetaRequirements 'Authorization headers' 'closed beta authorization header redaction warning'
Assert-Contains $closedBetaRequirements 'Do not present the build as a public beta or stable release' 'closed beta public/stable release warning'

Write-Host "Beta go/no-go validation passed for $resolvedPath"
