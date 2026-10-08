param(
    [string]$Root = '.'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$rootPath = (Resolve-Path -LiteralPath $Root).Path
$rootPrefix = $rootPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

$globalProductionRoots = @(
    'Sockseek.Api',
    'Sockseek.Application',
    'Sockseek.Cli',
    'Sockseek.Core',
    'Sockseek.Desktop',
    'Sockseek.Domain',
    'Sockseek.Infrastructure',
    'Sockseek.Integrations.Abstractions',
    'Sockseek.Integrations.Bandcamp',
    'Sockseek.Integrations.Spotify',
    'Sockseek.Integrations.YouTube',
    'Sockseek.Player',
    'Sockseek.Server'
)

$providerBoundaryRoots = @(
    'Sockseek.Api',
    'Sockseek.Application',
    'Sockseek.Desktop',
    'Sockseek.Domain',
    'Sockseek.Infrastructure',
    'Sockseek.Integrations.Abstractions',
    'Sockseek.Integrations.Bandcamp',
    'Sockseek.Integrations.Spotify',
    'Sockseek.Integrations.YouTube',
    'Sockseek.Player',
    'Sockseek.Server'
)

$fileExtensions = @('.cs', '.xaml', '.axaml', '.csproj')

$forbiddenContractPatterns = @(
    '\bIPlaybackProvider\b',
    '\bGetAudioStreamAsync\b',
    '\bDownloadTrackAsync\b',
    '\bAudioUrl\b',
    '\bAudioUri\b',
    '\bAudioStreamUrl\b',
    '\bStreamUrl\b',
    '\bPlaybackUrl\b',
    '\bPreviewUrl\b'
)

$forbiddenProviderBoundaryPatterns = @(
    '\byt-dlp\b',
    '\byoutube-dl\b',
    '\biframe\b'
)

function Get-ScannedFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$RelativeRoots
    )

    @(foreach ($relativeRoot in $RelativeRoots) {
        $absoluteRoot = Join-Path $rootPath $relativeRoot
        if (-not (Test-Path -LiteralPath $absoluteRoot)) {
            continue
        }

        Get-ChildItem -LiteralPath $absoluteRoot -Recurse -File |
            Where-Object {
                $fileExtensions -contains $_.Extension -and
                $_.FullName -notmatch '\\bin\\' -and
                $_.FullName -notmatch '\\obj\\'
            }
    })
}

function Find-ForbiddenPatterns {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo[]]$Files,

        [Parameter(Mandatory = $true)]
        [string[]]$Patterns
    )

    $scanMatches = @()
    if ($Files.Count -eq 0) {
        return $scanMatches
    }

    foreach ($pattern in $Patterns) {
        $scanMatches += Select-String -Path $Files.FullName -Pattern $pattern -AllMatches |
            ForEach-Object {
            $relativePath = $_.Path
            if ($relativePath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
                $relativePath = $relativePath.Substring($rootPrefix.Length)
            }

            [pscustomobject]@{
                Pattern = $pattern
                Path = $relativePath
                LineNumber = $_.LineNumber
                Line = $_.Line.Trim()
            }
        }
    }

    return $scanMatches
}

$globalFiles = Get-ScannedFiles $globalProductionRoots
$providerBoundaryFiles = Get-ScannedFiles $providerBoundaryRoots
$matches = @()
$matches += Find-ForbiddenPatterns $globalFiles $forbiddenContractPatterns
$matches += Find-ForbiddenPatterns $providerBoundaryFiles $forbiddenProviderBoundaryPatterns

if ($matches.Count -gt 0) {
    Write-Host "Forbidden provider-audio capability markers were found in production sources."
    $matches | Sort-Object Path, LineNumber, Pattern | Format-Table -AutoSize
    exit 1
}

Write-Host "Provider-audio guard passed. Scanned $($globalFiles.Count) production source files and $($providerBoundaryFiles.Count) provider-boundary files."
