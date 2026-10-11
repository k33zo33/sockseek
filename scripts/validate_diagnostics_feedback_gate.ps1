param(
    [string]$DiagnosticsPath = 'docs/diagnostics-feedback.md',
    [string]$BugTemplatePath = '.github/ISSUE_TEMPLATE/bug_report.md',
    [string]$CrashTemplatePath = '.github/ISSUE_TEMPLATE/crash_report.md',
    [string]$IssueConfigPath = '.github/ISSUE_TEMPLATE/config.yml',
    [string]$SecurityPath = 'SECURITY.md'
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

function Assert-SecretWarnings {
    param(
        [string]$Text,
        [string]$Description
    )

    $requiredWarnings = @(
        'tokens',
        'OAuth codes',
        'code verifiers',
        'client secrets',
        'Soulseek',
        'Authorization',
        'private playlist URLs'
    )

    foreach ($warning in $requiredWarnings) {
        Assert-Contains $Text $warning "$Description secret warning"
    }
}

$diagnostics = Read-RequiredText -Path $DiagnosticsPath -Description 'Diagnostics feedback document'
$bugTemplate = Read-RequiredText -Path $BugTemplatePath -Description 'Bug report template'
$crashTemplate = Read-RequiredText -Path $CrashTemplatePath -Description 'Crash report template'
$issueConfig = Read-RequiredText -Path $IssueConfigPath -Description 'Issue template config'
$security = Read-RequiredText -Path $SecurityPath -Description 'Security policy'

Assert-Contains $diagnostics 'closed beta feedback' 'diagnostics closed beta scope'
Assert-Contains $diagnostics 'Copy diagnostics' 'diagnostics Copy diagnostics guidance'
Assert-Contains $diagnostics 'smallest redacted diagnostics text' 'diagnostics minimal redacted excerpt guidance'
Assert-Contains $diagnostics 'GitHub crash report template' 'diagnostics crash template routing'
Assert-Contains $diagnostics '.github/ISSUE_TEMPLATE/bug_report.md' 'diagnostics bug template routing'
Assert-Contains $diagnostics '.github/ISSUE_TEMPLATE/crash_report.md' 'diagnostics crash template routing path'
Assert-Contains $diagnostics 'SECURITY.md' 'diagnostics security routing'
Assert-Contains $diagnostics 'Public beta remains blocked' 'diagnostics public beta blocker'
Assert-SecretWarnings $diagnostics 'diagnostics feedback'

Assert-Contains $bugTemplate 'Report a reproducible app problem without secrets' 'bug template no-secrets summary'
Assert-Contains $bugTemplate 'private vulnerability reporting' 'bug template security routing'
Assert-Contains $bugTemplate 'Version, commit or artifact name' 'bug template exact build evidence'
Assert-Contains $bugTemplate 'Provider involved' 'bug template provider surface'
Assert-Contains $bugTemplate 'Copy diagnostics' 'bug template Copy diagnostics guidance'
Assert-SecretWarnings $bugTemplate 'bug template'

Assert-Contains $crashTemplate 'Report an app, daemon or packaged-build crash without secrets' 'crash template no-secrets summary'
Assert-Contains $crashTemplate 'private vulnerability reporting' 'crash template security routing'
Assert-Contains $crashTemplate 'Version, commit or artifact name' 'crash template exact build evidence'
Assert-Contains $crashTemplate 'Docker/headless path' 'crash template Docker/headless surface'
Assert-Contains $crashTemplate 'Copy diagnostics' 'crash template Copy diagnostics guidance'
Assert-SecretWarnings $crashTemplate 'crash template'

Assert-Contains $issueConfig 'blank_issues_enabled: false' 'issue template blank issue lock'
Assert-Contains $issueConfig 'security/advisories/new' 'issue template private advisory link'
Assert-Contains $issueConfig 'Do not include secrets' 'issue template secret warning'

Assert-Contains $security 'Please report security issues privately' 'security private reporting guidance'
Assert-Contains $security 'Public beta is not released yet' 'security public beta status'
Assert-Contains $security 'private vulnerability reporting' 'security private vulnerability reporting'
Assert-Contains $security 'smallest redacted diagnostic export or log excerpt' 'security minimal redacted excerpt guidance'
Assert-SecretWarnings $security 'security policy'

Write-Host 'Diagnostics feedback gate validation passed.'
