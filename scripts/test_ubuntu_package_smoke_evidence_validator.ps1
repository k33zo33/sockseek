param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$validator = Join-Path $PSScriptRoot 'validate_ubuntu_package_smoke_evidence.ps1'
if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) {
    throw "Validator script was not found: $validator"
}

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("sockseek-ubuntu-package-evidence-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

function Write-Evidence {
    param(
        [string]$Path,
        [string]$ArtifactName,
        [string]$ArtifactHash,
        [string]$Commit,
        [string]$DistributionId = 'ubuntu',
        [string]$DistributionVersion = '24.04',
        [string]$Architecture = 'x86_64',
        [bool]$DaemonHealth = $true,
        [bool]$DesktopProtectedApi = $true
    )

    $evidence = [ordered]@{
        schemaVersion = 1
        recordedAtUtc = '2026-10-21T12:00:00Z'
        commit = $Commit
        version = '3.0.5'
        runtime = 'linux-x64'
        archive = $ArtifactName
        archiveSha256 = $ArtifactHash
        host = [ordered]@{
            distributionId = $DistributionId
            distributionVersion = $DistributionVersion
            architecture = $Architecture
            containerImage = 'sockseek:test'
        }
        checks = [ordered]@{
            archiveExtracted = $true
            requiredPayloadPresent = $true
            executableModes = $true
            daemonHealth = $DaemonHealth
            daemonProtectedApiRejectsAnonymous = $true
            daemonSoulseekAccountLifecycle = $true
            daemonSoulseekAccountRestartRestore = $true
            daemonSoulseekSecretRemovedAfterDisconnect = $true
            secretPermissions = $true
            secretEncryptedAtRest = $true
            desktopStartedPackagedDaemon = $true
            desktopDaemonHealth = $true
            desktopDaemonProtectedApiRejectsAnonymous = $DesktopProtectedApi
            loopbackOnly = $true
            installUninstall = $true
            userDataPreserved = $true
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

    throw "Expected Ubuntu package evidence validation to fail: $Name"
}

function New-TestArtifact {
    param(
        [string]$Path,
        [string]$Commit,
        [string]$License = 'AGPL-3.0'
    )

    $stage = Join-Path $tempRoot ([IO.Path]::GetFileNameWithoutExtension([IO.Path]::GetFileNameWithoutExtension($Path)))
    New-Item -ItemType Directory -Path $stage | Out-Null
    [ordered]@{
        version = '3.0.5'
        commit = $Commit
        sourceUrl = 'https://github.com/k33zo33/sockseek'
        license = $License
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stage 'release-metadata.json') -Encoding UTF8

    & tar -czf $Path -C $stage 'release-metadata.json'
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to create test archive: $Path"
    }
}

try {
    $commit = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
    $artifactPath = Join-Path $tempRoot 'Sockseek-linux-x64.tar.gz'
    New-TestArtifact -Path $artifactPath -Commit $commit
    $artifactHash = (Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $evidencePath = Join-Path $tempRoot 'package-smoke-evidence.json'

    Write-Evidence -Path $evidencePath -ArtifactName 'Sockseek-linux-x64.tar.gz' -ArtifactHash $artifactHash -Commit $commit
    & $validator -ArtifactPath $artifactPath -EvidencePath $evidencePath -RequiredHost Ubuntu2404X64 -ExpectedCommit $commit *> $null

    Write-Evidence -Path $evidencePath -ArtifactName 'Sockseek-linux-x64.tar.gz' -ArtifactHash ('0' * 64) -Commit $commit
    Assert-ValidationFails 'artifact hash mismatch' {
        & $validator -ArtifactPath $artifactPath -EvidencePath $evidencePath
    }

    Write-Evidence -Path $evidencePath -ArtifactName 'Sockseek-linux-x64.tar.gz' -ArtifactHash $artifactHash -Commit ('b' * 40)
    Assert-ValidationFails 'evidence commit mismatch' {
        & $validator -ArtifactPath $artifactPath -EvidencePath $evidencePath
    }

    Write-Evidence -Path $evidencePath -ArtifactName 'renamed-package.tar.gz' -ArtifactHash $artifactHash -Commit $commit
    Assert-ValidationFails 'artifact name mismatch' {
        & $validator -ArtifactPath $artifactPath -EvidencePath $evidencePath
    }

    Write-Evidence -Path $evidencePath -ArtifactName 'Sockseek-linux-x64.tar.gz' -ArtifactHash $artifactHash -Commit $commit -DistributionVersion '22.04'
    Assert-ValidationFails 'Ubuntu 22.04 substituted for Ubuntu 24.04' {
        & $validator -ArtifactPath $artifactPath -EvidencePath $evidencePath -RequiredHost Ubuntu2404X64
    }

    Write-Evidence -Path $evidencePath -ArtifactName 'Sockseek-linux-x64.tar.gz' -ArtifactHash $artifactHash -Commit $commit -DistributionId 'debian'
    Assert-ValidationFails 'different distribution substituted for Ubuntu' {
        & $validator -ArtifactPath $artifactPath -EvidencePath $evidencePath -RequiredHost Ubuntu2404X64
    }

    Write-Evidence -Path $evidencePath -ArtifactName 'Sockseek-linux-x64.tar.gz' -ArtifactHash $artifactHash -Commit $commit -Architecture 'aarch64'
    Assert-ValidationFails 'ARM64 substituted for x64' {
        & $validator -ArtifactPath $artifactPath -EvidencePath $evidencePath -RequiredHost Ubuntu2404X64
    }

    Write-Evidence -Path $evidencePath -ArtifactName 'Sockseek-linux-x64.tar.gz' -ArtifactHash $artifactHash -Commit $commit -DaemonHealth $false
    Assert-ValidationFails 'failed daemon health check' {
        & $validator -ArtifactPath $artifactPath -EvidencePath $evidencePath
    }

    Write-Evidence -Path $evidencePath -ArtifactName 'Sockseek-linux-x64.tar.gz' -ArtifactHash $artifactHash -Commit $commit -DesktopProtectedApi $false
    Assert-ValidationFails 'unprotected Desktop child daemon' {
        & $validator -ArtifactPath $artifactPath -EvidencePath $evidencePath
    }

    Write-Evidence -Path $evidencePath -ArtifactName 'Sockseek-linux-x64.tar.gz' -ArtifactHash $artifactHash -Commit $commit
    Assert-ValidationFails 'expected commit mismatch' {
        & $validator -ArtifactPath $artifactPath -EvidencePath $evidencePath -ExpectedCommit 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'
    }

    $unlicensedArtifactPath = Join-Path $tempRoot 'Sockseek-unlicensed-linux-x64.tar.gz'
    New-TestArtifact -Path $unlicensedArtifactPath -Commit $commit -License 'Proprietary'
    $unlicensedHash = (Get-FileHash -LiteralPath $unlicensedArtifactPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Evidence -Path $evidencePath -ArtifactName 'Sockseek-unlicensed-linux-x64.tar.gz' -ArtifactHash $unlicensedHash -Commit $commit
    Assert-ValidationFails 'artifact license mismatch' {
        & $validator -ArtifactPath $unlicensedArtifactPath -EvidencePath $evidencePath
    }

    Write-Host 'Ubuntu package smoke evidence validator tests passed.'
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
