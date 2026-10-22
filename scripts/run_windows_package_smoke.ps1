param(
    [string]$Version = '3.0.5',

    [string]$Commit = '',

    [string]$SourceUrl = 'https://github.com/k33zo33/sockseek',

    [string]$Runtime = 'win-x64',

    [string]$Configuration = 'Release',

    [string]$ArtifactsRoot = '',

    [string]$DotNetCli = 'dotnet',

    [ValidateSet('Any', 'Windows11X64')]
    [string]$RequiredHost = 'Any',

    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($Arguments -join ' ')"
    }
}

function New-RandomHex {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateRange(1, 1024)]
        [int]$ByteCount
    )

    $bytes = New-Object byte[] $ByteCount
    $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $generator.GetBytes($bytes)
        return [BitConverter]::ToString($bytes).Replace('-', '')
    }
    finally {
        $generator.Dispose()
    }
}

function Get-AvailableLoopbackPort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    try {
        $listener.Start()
        return ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

function Wait-ForHealth {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BaseUrl,

        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$Process,

        [int]$TimeoutSeconds = 90
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $Process.Refresh()
        if ($Process.HasExited) {
            throw "Process $($Process.Id) exited with code $($Process.ExitCode) before $BaseUrl/health became ready."
        }

        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/health" -TimeoutSec 5
            if ($response.StatusCode -eq 200 -and $response.Content -match '"status"\s*:\s*"ok"') {
                return
            }
        }
        catch {
            if ([DateTimeOffset]::UtcNow -ge $deadline) {
                throw
            }
        }

        Start-Sleep -Milliseconds 250
    } while ([DateTimeOffset]::UtcNow -lt $deadline)

    throw "Timed out waiting for $BaseUrl/health."
}

function Assert-ProtectedApiRejectsAnonymousRequest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BaseUrl
    )

    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/api/v1/system/info" -TimeoutSec 5
        throw "Protected API unexpectedly returned HTTP $($response.StatusCode) without a session token."
    }
    catch {
        $statusCode = $null
        if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }

        if ($statusCode -ne 401) {
            throw
        }
    }
}

function Configure-SoulseekAccountForRestartSmoke {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BaseUrl,

        [Parameter(Mandatory = $true)]
        [string]$SessionToken,

        [Parameter(Mandatory = $true)]
        [string]$SecretStoreDir
    )

    $username = "package-smoke-$([Guid]::NewGuid().ToString('N').Substring(0, 12))"
    $password = New-RandomHex -ByteCount 24
    $headers = @{ Authorization = "Bearer $SessionToken" }
    $configured = $false
    $completed = $false

    try {
        $requestBody = @{ username = $username; password = $password } | ConvertTo-Json -Compress
        $account = Invoke-RestMethod `
            -Method Put `
            -Uri "$BaseUrl/api/v1/soulseek/account" `
            -Headers $headers `
            -ContentType 'application/json' `
            -Body $requestBody `
            -TimeoutSec 30
        $configured = $true

        $serializedAccount = $account | ConvertTo-Json -Compress
        if (-not $account.isConfigured `
            -or $account.username -ne $username `
            -or $account.credentialStatus -ne 'configured' `
            -or $serializedAccount.IndexOf($password, [StringComparison]::Ordinal) -ge 0 `
            -or $serializedAccount.IndexOf('secretReference', [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw 'Packaged Soulseek account configure response was invalid or exposed secret data.'
        }

        $secretFiles = @(Get-ChildItem -LiteralPath $SecretStoreDir -File -Filter '*.secret' -Force)
        if ($secretFiles.Count -ne 1) {
            throw "Packaged Soulseek account expected one encrypted secret payload, found $($secretFiles.Count)."
        }

        $secretPayload = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($secretFiles[0].FullName))
        if ($secretPayload.IndexOf($password, [StringComparison]::Ordinal) -ge 0) {
            throw 'Packaged Soulseek secret payload contains the plaintext password.'
        }

        $completed = $true
        return $username
    }
    finally {
        if ($configured -and -not $completed) {
            try {
                Invoke-RestMethod `
                    -Method Delete `
                    -Uri "$BaseUrl/api/v1/soulseek/account" `
                    -Headers $headers `
                    -TimeoutSec 10 | Out-Null
            }
            catch {
                Write-Warning 'Packaged Soulseek account cleanup request failed.'
            }
        }

        $password = $null
        $requestBody = $null
    }
}

function Assert-RestoredSoulseekAccountAndDisconnect {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BaseUrl,

        [Parameter(Mandatory = $true)]
        [string]$SessionToken,

        [Parameter(Mandatory = $true)]
        [string]$Username,

        [Parameter(Mandatory = $true)]
        [string]$SecretStoreDir
    )

    $headers = @{ Authorization = "Bearer $SessionToken" }
    $account = Invoke-RestMethod `
        -Method Get `
        -Uri "$BaseUrl/api/v1/soulseek/account" `
        -Headers $headers `
        -TimeoutSec 30
    if (-not $account.isConfigured `
        -or $account.username -ne $Username `
        -or $account.credentialStatus -ne 'configured') {
        throw 'Packaged Soulseek account was not restored from the secret store after daemon restart.'
    }

    $account = Invoke-RestMethod `
        -Method Delete `
        -Uri "$BaseUrl/api/v1/soulseek/account" `
        -Headers $headers `
        -TimeoutSec 30
    if ($account.isConfigured -or $account.credentialStatus -ne 'not_configured') {
        throw 'Packaged Soulseek account disconnect did not clear account status.'
    }

    if (@(Get-ChildItem -LiteralPath $SecretStoreDir -File -Filter '*.secret' -Force).Count -ne 0) {
        throw 'Packaged Soulseek account disconnect did not remove the secret payload.'
    }
}

function Assert-ProcessListensOnlyOnLoopback {
    param(
        [Parameter(Mandatory = $true)]
        [int]$ProcessId
    )

    $listeners = @(Get-NetTCPConnection -State Listen -OwningProcess $ProcessId -ErrorAction Stop)
    if ($listeners.Count -eq 0) {
        throw "Process $ProcessId has no listening TCP endpoint."
    }

    $nonLoopback = @($listeners | Where-Object {
        $_.LocalAddress -ne '127.0.0.1' -and $_.LocalAddress -ne '::1'
    })
    if ($nonLoopback.Count -gt 0) {
        $addresses = ($nonLoopback | ForEach-Object { "$($_.LocalAddress):$($_.LocalPort)" }) -join ', '
        throw "Process $ProcessId exposed a non-loopback listener: $addresses"
    }
}

function Stop-SmokeProcess {
    param(
        [System.Diagnostics.Process]$Process
    )

    if ($null -eq $Process) {
        return
    }

    try {
        $Process.Refresh()
        if (-not $Process.HasExited) {
            Stop-Process -Id $Process.Id -Force -ErrorAction SilentlyContinue
            $Process.WaitForExit(10000) | Out-Null
        }
    }
    finally {
        $Process.Dispose()
    }
}

function Wait-ForDesktopDaemon {
    param(
        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$DesktopProcess,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedDaemonPath,

        [int]$TimeoutSeconds = 90
    )

    $expectedPath = [System.IO.Path]::GetFullPath($ExpectedDaemonPath)
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $DesktopProcess.Refresh()
        if ($DesktopProcess.HasExited) {
            throw "Packaged Desktop exited with code $($DesktopProcess.ExitCode) before its daemon became ready."
        }

        $children = @(Get-CimInstance Win32_Process -Filter "ParentProcessId = $($DesktopProcess.Id)" -ErrorAction SilentlyContinue)
        foreach ($child in $children) {
            if ([string]::IsNullOrWhiteSpace($child.ExecutablePath) -or
                (-not [System.IO.Path]::GetFullPath($child.ExecutablePath).Equals($expectedPath, [StringComparison]::OrdinalIgnoreCase))) {
                continue
            }

            $listeners = @(Get-NetTCPConnection -State Listen -OwningProcess $child.ProcessId -ErrorAction SilentlyContinue |
                Where-Object { $_.LocalAddress -eq '127.0.0.1' })
            foreach ($listener in $listeners) {
                $daemonProcess = Get-Process -Id $child.ProcessId -ErrorAction SilentlyContinue
                if ($null -eq $daemonProcess) {
                    continue
                }

                $baseUrl = "http://127.0.0.1:$($listener.LocalPort)"
                try {
                    Wait-ForHealth -BaseUrl $baseUrl -Process $daemonProcess -TimeoutSeconds 5
                    return [PSCustomObject]@{
                        Process = $daemonProcess
                        BaseUrl = $baseUrl
                    }
                }
                catch {
                    $daemonProcess.Dispose()
                }
            }
        }

        Start-Sleep -Milliseconds 250
    } while ([DateTimeOffset]::UtcNow -lt $deadline)

    throw 'Timed out waiting for the packaged Desktop child daemon on loopback.'
}

function Get-WindowsHostClassification {
    param($HostEvidence)

    $buildNumber = 0
    $hasBuildNumber = [int]::TryParse([string]$HostEvidence.BuildNumber, [ref]$buildNumber)
    $isX64 = [string]$HostEvidence.OSArchitecture -match '(?i)(64-bit|x64|amd64)'
    if ([string]$HostEvidence.Caption -match '(?i)Windows 11' `
        -and $isX64 `
        -and $hasBuildNumber `
        -and $buildNumber -ge 22000) {
        return 'Windows11X64'
    }

    if ([string]$HostEvidence.Caption -match '(?i)Windows 10' -and $isX64 -and $hasBuildNumber) {
        return 'Windows10X64'
    }

    return 'Other'
}

$os = Get-CimInstance Win32_OperatingSystem
$hostClassification = Get-WindowsHostClassification -HostEvidence $os
if ($RequiredHost -ne 'Any' -and $hostClassification -ne $RequiredHost) {
    throw "Windows package smoke requires host $RequiredHost, but this host is ${hostClassification}: $($os.Caption) $($os.Version) build $($os.BuildNumber) $($os.OSArchitecture)."
}

$headCommit = (& git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($headCommit)) {
    throw 'Unable to resolve the current Git commit.'
}

if ([string]::IsNullOrWhiteSpace($Commit)) {
    $Commit = $headCommit
}
elseif (-not $headCommit.StartsWith($Commit, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Requested commit $Commit is not the checked-out commit $headCommit."
}
else {
    $Commit = $headCommit
}

$workingTreeChanges = @(& git status --porcelain --untracked-files=all)
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to inspect the Git working tree.'
}
if ($workingTreeChanges.Count -gt 0) {
    throw 'Windows package smoke requires a clean working tree so the artifact matches its recorded commit.'
}

if ([string]::IsNullOrWhiteSpace($ArtifactsRoot)) {
    $ArtifactsRoot = ".tmp\sprint16-windows-package-smoke-$($headCommit.Substring(0, 7))"
}

$repoRoot = (Resolve-Path -LiteralPath '.').Path
$artifactsRootFullPath = [System.IO.Path]::GetFullPath($ArtifactsRoot)
$repositoryPrefix = $repoRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $artifactsRootFullPath.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "ArtifactsRoot must stay inside the repository: $artifactsRootFullPath"
}

if (Test-Path -LiteralPath $artifactsRootFullPath) {
    if (-not $Force) {
        throw "Artifacts root already exists: $artifactsRootFullPath. Pass -Force to replace it."
    }

    Remove-Item -LiteralPath $artifactsRootFullPath -Recurse -Force
}

$desktopPublishDir = Join-Path $artifactsRootFullPath "publish-desktop-$Runtime"
$daemonPublishDir = Join-Path $artifactsRootFullPath "publish-daemon-$Runtime"
$stageDir = Join-Path $artifactsRootFullPath "stage-$Runtime"
$sbomPath = Join-Path $artifactsRootFullPath 'release-sbom.spdx.json'
$archivePath = Join-Path $artifactsRootFullPath "Sockseek-$Runtime.zip"
$manifestPath = Join-Path $artifactsRootFullPath "Sockseek-$Runtime.sha256"
$extractedDir = Join-Path $artifactsRootFullPath "extracted-$Runtime"
$runtimeDataDir = Join-Path $artifactsRootFullPath 'runtime-data'
$daemonDataDir = Join-Path $runtimeDataDir 'daemon'
$desktopDataDir = Join-Path $runtimeDataDir 'desktop'
$evidencePath = Join-Path $artifactsRootFullPath 'package-smoke-evidence.json'
$testerInstructionsFileName = 'closed-beta-tester-instructions.md'
$testerInstructionsPath = Join-Path $stageDir $testerInstructionsFileName

New-Item -ItemType Directory -Force -Path $artifactsRootFullPath | Out-Null

Write-Host "Running Windows package smoke for commit $Commit."
Write-Host "Artifacts root: $artifactsRootFullPath"

Write-Host "Publishing Sockseek.Desktop for $Runtime."
Invoke-CheckedCommand $DotNetCli @(
    'publish',
    'Sockseek.Desktop\Sockseek.Desktop.csproj',
    '-c',
    $Configuration,
    '-r',
    $Runtime,
    '--self-contained',
    'true',
    '-o',
    $desktopPublishDir)

Write-Host "Publishing Sockseek.Server daemon for $Runtime."
Invoke-CheckedCommand $DotNetCli @(
    'publish',
    'Sockseek.Server\Sockseek.Server.csproj',
    '-c',
    $Configuration,
    '-r',
    $Runtime,
    '--self-contained',
    'true',
    '-o',
    $daemonPublishDir)

Write-Host "Generating SPDX SBOM."
Invoke-CheckedCommand $DotNetCli @(
    'run',
    '--project',
    'Sockseek.Packager\Sockseek.Packager.csproj',
    '-c',
    $Configuration,
    '--',
    'generate-sbom',
    $repoRoot,
    $sbomPath,
    'Sockseek',
    $Version,
    $Commit,
    $SourceUrl)

Write-Host "Staging Windows release layout."
Invoke-CheckedCommand $DotNetCli @(
    'run',
    '--project',
    'Sockseek.Packager\Sockseek.Packager.csproj',
    '-c',
    $Configuration,
    '--',
    'stage-windows',
    $repoRoot,
    $desktopPublishDir,
    $daemonPublishDir,
    $stageDir,
    'Sockseek.Desktop.exe',
    'Sockseek.Server.exe',
    $Version,
    $Commit,
    $SourceUrl,
    $sbomPath)

Write-Host "Generating closed beta tester instructions."
Invoke-CheckedCommand 'powershell' @(
    '-NoProfile',
    '-ExecutionPolicy',
    'Bypass',
    '-File',
    'scripts\write_closed_beta_tester_instructions.ps1',
    '-Commit',
    $Commit,
    '-SourceUrl',
    $SourceUrl,
    '-ArtifactName',
    "Sockseek $Runtime closed beta",
    '-Version',
    $Version,
    '-OutputPath',
    $testerInstructionsPath)

if (-not (Test-Path -LiteralPath $testerInstructionsPath)) {
    throw "Closed beta tester instructions were not generated: $testerInstructionsPath"
}

Write-Host "Creating Windows release archive and SHA256 manifest."
Invoke-CheckedCommand $DotNetCli @(
    'run',
    '--project',
    'Sockseek.Packager\Sockseek.Packager.csproj',
    '-c',
    $Configuration,
    '--',
    'archive-windows',
    $stageDir,
    $archivePath,
    $manifestPath,
    'Sockseek.Desktop.exe',
    'Sockseek.Server.exe')

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    if (-not ($archive.Entries | Where-Object { $_.FullName -eq $testerInstructionsFileName })) {
        throw "Windows release archive is missing $testerInstructionsFileName"
    }
}
finally {
    $archive.Dispose()
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw
if ($manifest -notmatch [Regex]::Escape($testerInstructionsFileName)) {
    throw "SHA256 manifest is missing $testerInstructionsFileName"
}

Write-Host 'Extracting and running the exact Windows release archive.'
Expand-Archive -LiteralPath $archivePath -DestinationPath $extractedDir

$extractedDesktopPath = Join-Path $extractedDir 'Sockseek.Desktop.exe'
$extractedDaemonPath = Join-Path $extractedDir 'daemon\Sockseek.Server.exe'
foreach ($requiredExecutable in @($extractedDesktopPath, $extractedDaemonPath)) {
    if (-not (Test-Path -LiteralPath $requiredExecutable -PathType Leaf)) {
        throw "Extracted archive is missing executable: $requiredExecutable"
    }
}

New-Item -ItemType Directory -Force -Path $daemonDataDir, $desktopDataDir | Out-Null
$originalServerConfigDir = [Environment]::GetEnvironmentVariable('SockseekServer__ConfigDir', 'Process')
$originalServerSessionToken = [Environment]::GetEnvironmentVariable('SockseekServer__SessionToken', 'Process')
$originalDesktopConfigDir = [Environment]::GetEnvironmentVariable('SOCKSEEK_DESKTOP_CONFIG_DIR', 'Process')
$daemonProcess = $null
$desktopProcess = $null
$desktopDaemon = $null
$directDaemonUrl = $null
$desktopDaemonUrl = $null

try {
    [Environment]::SetEnvironmentVariable('SockseekServer__ConfigDir', $daemonDataDir, 'Process')
    $smokeSessionToken = New-RandomHex -ByteCount 32
    [Environment]::SetEnvironmentVariable('SockseekServer__SessionToken', $smokeSessionToken, 'Process')
    $port = Get-AvailableLoopbackPort
    $directDaemonUrl = "http://127.0.0.1:$port"
    $daemonStdout = Join-Path $artifactsRootFullPath 'daemon.stdout.log'
    $daemonStderr = Join-Path $artifactsRootFullPath 'daemon.stderr.log'
    $daemonProcess = Start-Process -FilePath $extractedDaemonPath `
        -ArgumentList "--urls $directDaemonUrl" `
        -WorkingDirectory (Split-Path -Parent $extractedDaemonPath) `
        -RedirectStandardOutput $daemonStdout `
        -RedirectStandardError $daemonStderr `
        -WindowStyle Hidden `
        -PassThru

    Wait-ForHealth -BaseUrl $directDaemonUrl -Process $daemonProcess
    Assert-ProcessListensOnlyOnLoopback -ProcessId $daemonProcess.Id
    Assert-ProtectedApiRejectsAnonymousRequest -BaseUrl $directDaemonUrl
    $smokeAccountUsername = Configure-SoulseekAccountForRestartSmoke `
        -BaseUrl $directDaemonUrl `
        -SessionToken $smokeSessionToken `
        -SecretStoreDir (Join-Path $daemonDataDir 'secrets')
    Stop-SmokeProcess -Process $daemonProcess
    $daemonProcess = $null

    $daemonRestartStdout = Join-Path $artifactsRootFullPath 'daemon-restart.stdout.log'
    $daemonRestartStderr = Join-Path $artifactsRootFullPath 'daemon-restart.stderr.log'
    $daemonProcess = Start-Process -FilePath $extractedDaemonPath `
        -ArgumentList "--urls $directDaemonUrl" `
        -WorkingDirectory (Split-Path -Parent $extractedDaemonPath) `
        -RedirectStandardOutput $daemonRestartStdout `
        -RedirectStandardError $daemonRestartStderr `
        -WindowStyle Hidden `
        -PassThru

    Wait-ForHealth -BaseUrl $directDaemonUrl -Process $daemonProcess
    Assert-ProcessListensOnlyOnLoopback -ProcessId $daemonProcess.Id
    Assert-ProtectedApiRejectsAnonymousRequest -BaseUrl $directDaemonUrl
    Assert-RestoredSoulseekAccountAndDisconnect `
        -BaseUrl $directDaemonUrl `
        -SessionToken $smokeSessionToken `
        -Username $smokeAccountUsername `
        -SecretStoreDir (Join-Path $daemonDataDir 'secrets')
    Write-Host 'Exact packaged daemon restored and removed the secret-backed Soulseek account after restart.'
    Write-Host "Exact packaged daemon is healthy and keeps /api/v1 protected at $directDaemonUrl."
    Stop-SmokeProcess -Process $daemonProcess
    $daemonProcess = $null

    [Environment]::SetEnvironmentVariable('SockseekServer__ConfigDir', $null, 'Process')
    [Environment]::SetEnvironmentVariable('SockseekServer__SessionToken', $null, 'Process')
    [Environment]::SetEnvironmentVariable('SOCKSEEK_DESKTOP_CONFIG_DIR', $desktopDataDir, 'Process')
    $desktopStdout = Join-Path $artifactsRootFullPath 'desktop.stdout.log'
    $desktopStderr = Join-Path $artifactsRootFullPath 'desktop.stderr.log'
    $escapedRepoRoot = $repoRoot.Replace('"', '\"')
    $desktopProcess = Start-Process -FilePath $extractedDesktopPath `
        -ArgumentList "--headless --workspace-root `"$escapedRepoRoot`"" `
        -WorkingDirectory $extractedDir `
        -RedirectStandardOutput $desktopStdout `
        -RedirectStandardError $desktopStderr `
        -WindowStyle Hidden `
        -PassThru

    $desktopDaemon = Wait-ForDesktopDaemon `
        -DesktopProcess $desktopProcess `
        -ExpectedDaemonPath $extractedDaemonPath
    $desktopDaemonUrl = $desktopDaemon.BaseUrl
    Assert-ProcessListensOnlyOnLoopback -ProcessId $desktopDaemon.Process.Id
    Assert-ProtectedApiRejectsAnonymousRequest -BaseUrl $desktopDaemonUrl
    Write-Host "Exact packaged Desktop launched its healthy protected daemon at $desktopDaemonUrl."
}
finally {
    if ($null -ne $desktopDaemon) {
        Stop-SmokeProcess -Process $desktopDaemon.Process
    }
    Stop-SmokeProcess -Process $desktopProcess
    Stop-SmokeProcess -Process $daemonProcess
    [Environment]::SetEnvironmentVariable('SockseekServer__ConfigDir', $originalServerConfigDir, 'Process')
    [Environment]::SetEnvironmentVariable('SockseekServer__SessionToken', $originalServerSessionToken, 'Process')
    [Environment]::SetEnvironmentVariable('SOCKSEEK_DESKTOP_CONFIG_DIR', $originalDesktopConfigDir, 'Process')
}

$archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
$evidence = [ordered]@{
    schemaVersion = 1
    recordedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    commit = $headCommit
    version = $Version
    runtime = $Runtime
    archive = [System.IO.Path]::GetFileName($archivePath)
    archiveSha256 = $archiveHash
    host = [ordered]@{
        caption = $os.Caption
        version = $os.Version
        buildNumber = $os.BuildNumber
        osArchitecture = $os.OSArchitecture
    }
    checks = [ordered]@{
        archiveExtracted = $true
        daemonHealth = $true
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
$evidence | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $evidencePath -Encoding UTF8

$validator = Join-Path $PSScriptRoot 'validate_windows_package_smoke_evidence.ps1'
& $validator `
    -ArtifactPath $archivePath `
    -EvidencePath $evidencePath `
    -RequiredHost $RequiredHost `
    -ExpectedCommit $headCommit

Write-Host "Windows package smoke completed successfully."
Write-Host "Commit: $headCommit"
Write-Host "Host: $($os.Caption) $($os.Version) $($os.OSArchitecture)"
Write-Host "Stage: $stageDir"
Write-Host "Archive: $archivePath"
Write-Host "Archive SHA256: $archiveHash"
Write-Host "Manifest: $manifestPath"
Write-Host "Tester instructions: $testerInstructionsPath"
Write-Host "Evidence: $evidencePath"
