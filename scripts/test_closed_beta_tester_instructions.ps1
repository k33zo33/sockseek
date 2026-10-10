param(
    [string]$Commit = 'testcommit'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$generator = Join-Path $PSScriptRoot 'write_closed_beta_tester_instructions.ps1'
if (-not (Test-Path -LiteralPath $generator)) {
    throw "Closed beta tester instructions generator was not found: $generator"
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("sockseek-closed-beta-instructions-" + [System.Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

try {
    $outputPath = Join-Path $tempRoot 'closed-beta-tester-instructions.md'
    & powershell -NoProfile -ExecutionPolicy Bypass -File $generator `
        -Commit $Commit `
        -ArtifactName 'Sockseek Windows closed beta smoke' `
        -Version '3.0.5-smoke' `
        -OutputPath $outputPath *> $null

    if ($LASTEXITCODE -ne 0) {
        throw "Closed beta tester instructions generator failed with exit code $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $outputPath)) {
        throw "Closed beta tester instructions were not written: $outputPath"
    }

    $instructions = Get-Content -LiteralPath $outputPath -Raw
    $requiredText = @(
        "# Sockseek closed/internal beta tester instructions",
        "| Commit | $Commit |",
        'This build is for closed/internal testing only. Do not present it as a public beta or stable release.',
        'Public beta remains blocked until docs/beta-go-no-go.md says public beta is GO for this exact commit or tag.',
        'Soulseek compliance is unresolved under ADR-0009',
        'External services are playlist or metadata sources only.',
        'Spotify, YouTube, Bandcamp and MusicBrainz are never audio sources',
        'Docker smoke helper now fails hung Docker CLI commands with a bounded timeout and records Windows Docker Desktop pipe/config diagnostics',
        'Use the app Copy diagnostics action when available.',
        'Never paste provider tokens, OAuth codes, PKCE code verifiers, client secrets, Soulseek passwords, full Authorization headers or private playlist URLs with sensitive query parameters.'
    )

    foreach ($text in $requiredText) {
        if ($instructions.IndexOf($text, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw "Closed beta tester instructions are missing expected text: $text"
        }
    }

    Write-Host 'Closed beta tester instructions smoke passed.'
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
