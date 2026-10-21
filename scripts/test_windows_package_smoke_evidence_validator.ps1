param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$validator = Join-Path $PSScriptRoot 'validate_windows_package_smoke_evidence.ps1'
if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) {
    throw "Validator script was not found: $validator"
}

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("sockseek-windows-package-evidence-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

function Write-Evidence {
    param(
        [string]$Path,
        [string]$ArtifactName,
        [string]$ArtifactHash,
        [string]$Commit,
        [string]$Caption = 'Microsoft Windows 11 Pro',
        [string]$BuildNumber = '22631',
        [bool]$DaemonHealth = $true
    )

    $evidence = [ordered]@{
        schemaVersion = 1
        recordedAtUtc = '2026-10-21T12:00:00Z'
        commit = $Commit
        version = '3.0.5'
        runtime = 'win-x64'
        archive = $ArtifactName
        archiveSha256 = $ArtifactHash
        host = [ordered]@{
            caption = $Caption
            version = "10.0.$BuildNumber"
            buildNumber = $BuildNumber
            osArchitecture = '64-bit'
        }
        checks = [ordered]@{
            archiveExtracted = $true
            daemonHealth = $DaemonHealth
            daemonProtectedApiRejectsAnonymous = $true
            daemonSoulseekAccountLifecycle = $true
            daemonSoulseekAccountRestartRestore = $true
            daemonSoulseekSecretRemovedAfterDisconnect = $true
            desktopStartedPackagedDaemon = $true
            desktopDaemonHealth = $true
            desktopDaemonProtectedApiRejectsAnonymous = $true
            loopbackOnly = $true
        }
    }
    $evidence | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Assert-ValidationFails {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    try {
        & $Action *> $null
    }
    catch {
        return
    }

    throw "Expected Windows package evidence validation to fail: $Name"
}

try {
    $commit = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
    $stage = Join-Path $tempRoot 'stage'
    New-Item -ItemType Directory -Path $stage | Out-Null
    $releaseMetadataPath = Join-Path $stage 'release-metadata.json'
    [ordered]@{
        version = '3.0.5'
        commit = $commit
        sourceUrl = 'https://github.com/k33zo33/sockseek'
        license = 'AGPL-3.0'
    } | ConvertTo-Json | Set-Content -LiteralPath $releaseMetadataPath -Encoding UTF8

    $artifactPath = Join-Path $tempRoot 'Sockseek-win-x64.zip'
    Compress-Archive -LiteralPath $releaseMetadataPath -DestinationPath $artifactPath
    $artifactHash = (Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $evidencePath = Join-Path $tempRoot 'package-smoke-evidence.json'

    Write-Evidence -Path $evidencePath -ArtifactName 'Sockseek-win-x64.zip' -ArtifactHash $artifactHash -Commit $commit
    & $validator -ArtifactPath $artifactPath -EvidencePath $evidencePath -RequiredHost Windows11X64 -ExpectedCommit $commit *> $null

    Write-Evidence -Path $evidencePath -ArtifactName 'Sockseek-win-x64.zip' -ArtifactHash ('0' * 64) -Commit $commit
    Assert-ValidationFails 'artifact hash mismatch' {
        & $validator -ArtifactPath $artifactPath -EvidencePath $evidencePath
    }

    Write-Evidence -Path $evidencePath -ArtifactName 'Sockseek-win-x64.zip' -ArtifactHash $artifactHash -Commit ('b' * 40)
    Assert-ValidationFails 'evidence commit mismatch' {
        & $validator -ArtifactPath $artifactPath -EvidencePath $evidencePath
    }

    Write-Evidence -Path $evidencePath -ArtifactName 'renamed-package.zip' -ArtifactHash $artifactHash -Commit $commit
    Assert-ValidationFails 'artifact name mismatch' {
        & $validator -ArtifactPath $artifactPath -EvidencePath $evidencePath
    }

    Write-Evidence -Path $evidencePath -ArtifactName 'Sockseek-win-x64.zip' -ArtifactHash $artifactHash -Commit $commit -Caption 'Microsoft Windows 10 Pro' -BuildNumber '19045'
    Assert-ValidationFails 'Windows 10 substituted for Windows 11' {
        & $validator -ArtifactPath $artifactPath -EvidencePath $evidencePath -RequiredHost Windows11X64
    }

    Write-Evidence -Path $evidencePath -ArtifactName 'Sockseek-win-x64.zip' -ArtifactHash $artifactHash -Commit $commit -BuildNumber '19045'
    Assert-ValidationFails 'Windows 11 caption with an invalid build' {
        & $validator -ArtifactPath $artifactPath -EvidencePath $evidencePath -RequiredHost Windows11X64
    }

    Write-Evidence -Path $evidencePath -ArtifactName 'Sockseek-win-x64.zip' -ArtifactHash $artifactHash -Commit $commit -DaemonHealth $false
    Assert-ValidationFails 'failed daemon health check' {
        & $validator -ArtifactPath $artifactPath -EvidencePath $evidencePath
    }

    Write-Evidence -Path $evidencePath -ArtifactName 'Sockseek-win-x64.zip' -ArtifactHash $artifactHash -Commit $commit
    Assert-ValidationFails 'expected commit mismatch' {
        & $validator -ArtifactPath $artifactPath -EvidencePath $evidencePath -ExpectedCommit 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'
    }

    $unlicensedMetadataPath = Join-Path $stage 'unlicensed-release-metadata.json'
    [ordered]@{
        version = '3.0.5'
        commit = $commit
        sourceUrl = 'https://github.com/k33zo33/sockseek'
        license = 'Proprietary'
    } | ConvertTo-Json | Set-Content -LiteralPath $unlicensedMetadataPath -Encoding UTF8
    $unlicensedStage = Join-Path $tempRoot 'unlicensed-stage'
    New-Item -ItemType Directory -Path $unlicensedStage | Out-Null
    Copy-Item -LiteralPath $unlicensedMetadataPath -Destination (Join-Path $unlicensedStage 'release-metadata.json')
    $unlicensedArtifactPath = Join-Path $tempRoot 'Sockseek-unlicensed-win-x64.zip'
    Compress-Archive -LiteralPath (Join-Path $unlicensedStage 'release-metadata.json') -DestinationPath $unlicensedArtifactPath
    $unlicensedHash = (Get-FileHash -LiteralPath $unlicensedArtifactPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Evidence -Path $evidencePath -ArtifactName 'Sockseek-unlicensed-win-x64.zip' -ArtifactHash $unlicensedHash -Commit $commit
    Assert-ValidationFails 'artifact license mismatch' {
        & $validator -ArtifactPath $unlicensedArtifactPath -EvidencePath $evidencePath
    }

    Write-Host 'Windows package smoke evidence validator tests passed.'
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
