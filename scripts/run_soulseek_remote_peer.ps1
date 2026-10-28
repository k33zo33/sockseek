param(
    [Parameter(Mandatory = $true)]
    [string]$PublishedServerPath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9._-]{3,64}$')]
    [string]$RunLabel,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$CandidateCommit,

    [Parameter(Mandatory = $true)]
    [string]$EvidencePath,

    [ValidateRange(1, 65535)]
    [int]$ApiPort = 63003,

    [ValidateRange(1, 65535)]
    [int]$ListenPort = 50000,

    [ValidateRange(5, 20)]
    [int]$TimeoutMinutes = 18,

    [switch]$ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-ControlBundle {
    $raw = [Environment]::GetEnvironmentVariable('SOCKSEEK_LIVE_PEER_BUNDLE')
    if ([string]::IsNullOrWhiteSpace($raw)) {
        throw 'The controlled-peer bundle is unavailable.'
    }

    try {
        $value = $raw | ConvertFrom-Json
    }
    catch {
        throw 'The controlled-peer bundle is invalid.'
    }
    finally {
        $raw = $null
    }

    $required = @(
        'remoteUsername',
        'remotePassword',
        'hostUsername',
        'remoteProbe',
        'hostProbe',
        'readyMessage',
        'completeMessage'
    )
    foreach ($name in $required) {
        $property = $value.PSObject.Properties[$name]
        if (-not $property -or [string]::IsNullOrWhiteSpace([string]$property.Value)) {
            throw 'The controlled-peer bundle is incomplete.'
        }
        if ([string]$property.Value -match "[`r`n]") {
            throw 'The controlled-peer bundle contains an invalid multiline value.'
        }
    }

    if (([string]$value.remoteUsername -notmatch '^[A-Za-z0-9._-]{3,64}$') `
        -or ([string]$value.hostUsername -notmatch '^[A-Za-z0-9._-]{3,64}$')) {
        throw 'The controlled-peer identities are invalid.'
    }
    if (([string]$value.remoteProbe -notmatch '^[A-Za-z0-9._-]{8,96}$') `
        -or ([string]$value.hostProbe -notmatch '^[A-Za-z0-9._-]{8,96}$')) {
        throw 'The controlled-peer probes are invalid.'
    }

    return $value
}

function New-RandomHex {
    param([int]$ByteCount)

    $bytes = New-Object byte[] $ByteCount
    $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $generator.GetBytes($bytes)
    }
    finally {
        $generator.Dispose()
    }
    return ([BitConverter]::ToString($bytes)).Replace('-', '').ToLowerInvariant()
}

function New-ProbeWaveFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    $sampleRate = 8000
    $dataLength = $sampleRate * 2
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $writer = New-Object IO.BinaryWriter($stream)
        try {
            $writer.Write([Text.Encoding]::ASCII.GetBytes('RIFF'))
            $writer.Write([int](36 + $dataLength))
            $writer.Write([Text.Encoding]::ASCII.GetBytes('WAVE'))
            $writer.Write([Text.Encoding]::ASCII.GetBytes('fmt '))
            $writer.Write([int]16)
            $writer.Write([int16]1)
            $writer.Write([int16]1)
            $writer.Write([int]$sampleRate)
            $writer.Write([int]($sampleRate * 2))
            $writer.Write([int16]2)
            $writer.Write([int16]16)
            $writer.Write([Text.Encoding]::ASCII.GetBytes('data'))
            $writer.Write([int]$dataLength)
            $writer.Write((New-Object byte[] $dataLength))
        }
        finally {
            $writer.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Wait-ForCondition {
    param(
        [Parameter(Mandatory = $true)][scriptblock]$Condition,
        [int]$TimeoutSeconds = 120,
        [int]$DelayMilliseconds = 1000
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        try {
            $value = & $Condition
            if ($null -ne $value -and $value -ne $false) {
                return $value
            }
        }
        catch {
            # Startup, reconnect and not-found responses are transient in bounded waits.
        }
        Start-Sleep -Milliseconds $DelayMilliseconds
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    return $null
}

function Get-BoundedTimeout {
    param([int]$RequestedSeconds)

    $remaining = [int][Math]::Floor(($script:runDeadline - [DateTimeOffset]::UtcNow).TotalSeconds)
    if ($remaining -le 0) {
        return 1
    }
    return [Math]::Max(1, [Math]::Min($RequestedSeconds, $remaining))
}

$bundle = Read-ControlBundle
[Environment]::SetEnvironmentVariable('SOCKSEEK_LIVE_PEER_BUNDLE', $null)
if ($ValidateOnly) {
    $bundle = $null
    Write-Host 'Controlled Soulseek peer configuration validation passed.'
    exit 0
}

$resolvedServer = (Resolve-Path -LiteralPath $PublishedServerPath -ErrorAction Stop).Path
$evidenceDirectory = Split-Path -Parent ([IO.Path]::GetFullPath($EvidencePath))
New-Item -ItemType Directory -Path $evidenceDirectory -Force | Out-Null

$workRoot = Join-Path ([IO.Path]::GetTempPath()) ('sockseek-live-peer-' + $RunLabel)
$configDirectory = Join-Path $workRoot 'config'
$shareDirectory = Join-Path $workRoot 'share'
New-Item -ItemType Directory -Path $configDirectory, $shareDirectory -Force | Out-Null

$baseUrl = "http://127.0.0.1:$ApiPort"
$accessValue = New-RandomHex -ByteCount 32
$process = $null
$wishlistId = $null
$stdoutCopy = $null
$stderrCopy = $null
$replyMessage = $null
$stage = 'initialize'
$startedAt = [DateTimeOffset]::UtcNow
$script:runDeadline = $startedAt.AddMinutes($TimeoutMinutes)
$result = [ordered]@{
    schemaVersion = 1
    runLabel = $RunLabel
    candidateCommit = $CandidateCommit.ToLowerInvariant()
    startedAtUtc = $startedAt.ToString('o')
    completedAtUtc = $null
    exactCandidateSource = $true
    connected = $false
    sharing = [ordered]@{ passed = $false; directoryCount = 0; fileCount = 0 }
    readyHandshake = [ordered]@{ passed = $false; observedAtUtc = $null }
    search = [ordered]@{ passed = $false; observedAtUtc = $null; controlledResultCount = 0 }
    wishlist = [ordered]@{ passed = $false; observedAtUtc = $null; controlledResultCount = 0 }
    folderBrowse = [ordered]@{ passed = $false; observedAtUtc = $null; fullyRetrieved = $false }
    download = [ordered]@{ passed = $false; observedAtUtc = $null; terminalOutcome = $null }
    upload = [ordered]@{ passed = $false; observedAtUtc = $null; completedUploadDelta = 0 }
    chat = [ordered]@{ passed = $false; observedAtUtc = $null; replySent = $false; completionReceived = $false; publicToggleObserved = $false; roomRefreshObserved = $false }
    privileges = [ordered]@{ passed = $false; observedAtUtc = $null; refreshed = $false }
    cleanup = [ordered]@{ accountDisconnected = $false; daemonStopped = $false; workingDataRemoved = $false }
    failureStage = $null
    failureCode = $null
}

function Invoke-LocalApi {
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Path,
        [object]$Body = $null
    )

    $parameters = @{
        Uri = $baseUrl + $Path
        Method = $Method
        Headers = @{ Authorization = 'Bearer ' + $accessValue }
        TimeoutSec = 30
    }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = $Body | ConvertTo-Json -Depth 12 -Compress
    }
    try {
        return Invoke-RestMethod @parameters
    }
    catch {
        throw 'local_api_request_failed'
    }
}

try {
    $stage = 'start_daemon'
    $probeFile = Join-Path $shareDirectory ([string]$bundle.remoteProbe + '.wav')
    New-ProbeWaveFile -Path $probeFile

    $startInfo = New-Object Diagnostics.ProcessStartInfo
    if ([IO.Path]::GetExtension($resolvedServer) -eq '.dll') {
        $startInfo.FileName = 'dotnet'
        $startInfo.Arguments = '"' + $resolvedServer + '" --urls ' + $baseUrl
        $startInfo.WorkingDirectory = Split-Path -Parent $resolvedServer
    }
    else {
        $startInfo.FileName = $resolvedServer
        $startInfo.Arguments = '--urls ' + $baseUrl
        $startInfo.WorkingDirectory = Split-Path -Parent $resolvedServer
    }
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.EnvironmentVariables['SockseekServer__ConfigDir'] = $configDirectory
    $startInfo.EnvironmentVariables['SockseekServer__SessionToken'] = $accessValue
    $startInfo.EnvironmentVariables['SockseekServer__Engine__ListenPort'] = [string]$ListenPort
    $startInfo.EnvironmentVariables['SockseekServer__DefaultDownload__Output__ParentDir'] = (Join-Path $workRoot 'downloads')
    $startInfo.EnvironmentVariables['SOCKSEEK_DESKTOP_HANDSHAKE_STDOUT'] = '0'
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw 'daemon_start_failed'
    }
    $stdoutCopy = $process.StandardOutput.ReadToEndAsync()
    $stderrCopy = $process.StandardError.ReadToEndAsync()

    $healthy = Wait-ForCondition -TimeoutSeconds (Get-BoundedTimeout 90) -Condition {
        try {
            $health = Invoke-RestMethod -Uri ($baseUrl + '/health') -TimeoutSec 3
            return $health.status -eq 'ok'
        }
        catch {
            return $false
        }
    }
    if (-not $healthy) {
        throw 'daemon_health_timeout'
    }

    $stage = 'configure_account'
    $null = Invoke-LocalApi -Method Put -Path '/api/v1/soulseek/account' -Body @{
        username = [string]$bundle.remoteUsername
        password = [string]$bundle.remotePassword
    }
    $sharing = Invoke-LocalApi -Method Post -Path '/api/v1/soulseek/sharing/roots' -Body @{
        path = $shareDirectory
        virtualName = 'LiveEvidence'
        enabled = $true
    }
    $result.sharing.directoryCount = [int]$sharing.directoryCount
    $result.sharing.fileCount = [int]$sharing.fileCount
    $result.sharing.passed = $sharing.fileCount -ge 1 -and @($sharing.roots | Where-Object { $_.included }).Count -eq 1
    if (-not $result.sharing.passed) {
        throw 'share_snapshot_invalid'
    }

    $null = Invoke-LocalApi -Method Post -Path '/api/jobs/search' -Body @{ queryText = 'remote peer connect test' }
    $connected = Wait-ForCondition -TimeoutSeconds (Get-BoundedTimeout 120) -Condition {
        $state = Invoke-LocalApi -Method Get -Path '/api/v1/soulseek/account'
        return $state.isConnected
    }
    if (-not $connected) {
        throw 'soulseek_connection_timeout'
    }
    $result.connected = $true
    $uploadBefore = Invoke-LocalApi -Method Get -Path '/api/v1/soulseek/uploads'

    $stage = 'await_ready_message'
    $ready = Wait-ForCondition -TimeoutSeconds (Get-BoundedTimeout ($TimeoutMinutes * 60)) -DelayMilliseconds 2000 -Condition {
        $chatState = Invoke-LocalApi -Method Get -Path '/api/v1/soulseek/chat'
        @($chatState.messages | Where-Object {
            -not $_.isOutgoing `
                -and $_.username -eq [string]$bundle.hostUsername `
                -and $_.message -eq [string]$bundle.readyMessage
        }).Count -gt 0
    }
    if (-not $ready) {
        throw 'ready_message_timeout'
    }
    $result.readyHandshake.passed = $true
    $result.readyHandshake.observedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')

    $stage = 'bidirectional_chat'
    $replyMessage = 'reply-' + (New-RandomHex -ByteCount 8)
    $null = Invoke-LocalApi -Method Post -Path '/api/v1/soulseek/chat/private/messages' -Body @{
        username = [string]$bundle.hostUsername
        message = $replyMessage
    }
    $result.chat.replySent = $true
    $rooms = Invoke-LocalApi -Method Post -Path '/api/v1/soulseek/chat/rooms/refresh'
    $publicStarted = Invoke-LocalApi -Method Post -Path '/api/v1/soulseek/chat/public/start'
    $publicStopped = Invoke-LocalApi -Method Post -Path '/api/v1/soulseek/chat/public/stop'
    $result.chat.roomRefreshObserved = $null -ne $rooms.availableRooms
    $result.chat.publicToggleObserved = $publicStarted.publicChatEnabled -and -not $publicStopped.publicChatEnabled
    if (-not $result.chat.roomRefreshObserved -or -not $result.chat.publicToggleObserved) {
        throw 'chat_actions_failed'
    }

    $stage = 'controlled_search'
    $searchJob = Invoke-LocalApi -Method Post -Path '/api/jobs/search' -Body @{ queryText = [string]$bundle.hostProbe }
    $searchSnapshot = Wait-ForCondition -TimeoutSeconds (Get-BoundedTimeout 150) -DelayMilliseconds 1500 -Condition {
        $snapshot = Invoke-LocalApi -Method Get -Path ('/api/jobs/' + $searchJob.jobId + '/results/files')
        $controlled = @($snapshot.items | Where-Object {
            $_.username -eq [string]$bundle.hostUsername `
                -and $_.filename -like ('*' + [string]$bundle.hostProbe + '*')
        })
        if ($controlled.Count -gt 0) {
            return [pscustomobject]@{ candidates = $controlled }
        }
        return $null
    }
    if (-not $searchSnapshot) {
        throw 'controlled_search_timeout'
    }
    $candidate = @($searchSnapshot.candidates)[0]
    $result.search.passed = $true
    $result.search.controlledResultCount = @($searchSnapshot.candidates).Count
    $result.search.observedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')

    $stage = 'controlled_wishlist'
    $wishlist = Invoke-LocalApi -Method Post -Path '/api/v1/soulseek/wishlist' -Body @{
        query = [string]$bundle.hostProbe
        intervalMinutes = 30
        enabled = $true
    }
    $wishlistId = [string]$wishlist.id
    $null = Invoke-LocalApi -Method Post -Path ('/api/v1/soulseek/wishlist/' + $wishlistId + '/run')
    $wishlistDone = Wait-ForCondition -TimeoutSeconds (Get-BoundedTimeout 150) -DelayMilliseconds 1500 -Condition {
        $entriesResponse = Invoke-LocalApi -Method Get -Path '/api/v1/soulseek/wishlist'
        $entries = @($entriesResponse | ForEach-Object { $_ })
        $entry = $entries | Where-Object { [string]$_.id -eq $wishlistId } | Select-Object -First 1
        $completedProperty = if ($entry) { $entry.PSObject.Properties['lastSearchCompletedAtUtc'] } else { $null }
        if ($completedProperty -and $null -ne $completedProperty.Value) {
            return $entry
        }
        return $null
    }
    $wishlistCountProperty = if ($wishlistDone) { $wishlistDone.PSObject.Properties['lastResultCount'] } else { $null }
    $wishlistCount = if ($wishlistCountProperty) { [int]$wishlistCountProperty.Value } else { 0 }
    if (-not $wishlistDone -or $wishlistCount -lt 1) {
        throw 'controlled_wishlist_timeout'
    }
    $result.wishlist.passed = $true
    $result.wishlist.controlledResultCount = $wishlistCount
    $result.wishlist.observedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')

    $stage = 'controlled_folder_browse'
    $folders = Invoke-LocalApi -Method Get -Path ('/api/jobs/' + $searchJob.jobId + '/results/folders?includeFiles=true')
    $folder = @($folders.items | Where-Object { $_.username -eq [string]$bundle.hostUsername }) | Select-Object -First 1
    if (-not $folder) {
        throw 'controlled_folder_missing'
    }
    $retrieveJob = Invoke-LocalApi -Method Post -Path ('/api/jobs/' + $searchJob.jobId + '/retrieve-folder') -Body @{
        folder = @{ username = [string]$bundle.hostUsername; folderPath = [string]$folder.folderPath }
    }
    $retrieveDone = Wait-ForCondition -TimeoutSeconds (Get-BoundedTimeout 120) -Condition {
        $detail = Invoke-LocalApi -Method Get -Path ('/api/jobs/' + $retrieveJob.jobId)
        if ([string]$detail.summary.lifecycleState -eq 'Terminal') {
            return $detail
        }
        return $null
    }
    if (-not $retrieveDone -or [string]$retrieveDone.summary.terminalOutcome -ne 'Succeeded') {
        throw 'controlled_folder_retrieval_failed'
    }
    $foldersAfter = Invoke-LocalApi -Method Get -Path ('/api/jobs/' + $searchJob.jobId + '/results/folders?includeFiles=true')
    $retrieved = @($foldersAfter.items | Where-Object {
        $_.username -eq [string]$bundle.hostUsername -and $_.isFullyRetrieved
    }) | Select-Object -First 1
    if (-not $retrieved) {
        throw 'controlled_folder_not_merged'
    }
    $result.folderBrowse.passed = $true
    $result.folderBrowse.fullyRetrieved = $true
    $result.folderBrowse.observedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')

    $stage = 'controlled_download'
    $downloadResponse = Invoke-LocalApi -Method Post -Path ('/api/jobs/' + $searchJob.jobId + '/downloads/files') -Body @{
        files = @(@{ username = [string]$candidate.ref.username; filename = [string]$candidate.ref.filename })
    }
    $downloads = @($downloadResponse | ForEach-Object { $_ })
    if ($downloads.Count -ne 1) {
        throw 'controlled_download_not_created'
    }
    $downloadDone = Wait-ForCondition -TimeoutSeconds (Get-BoundedTimeout 180) -Condition {
        $detail = Invoke-LocalApi -Method Get -Path ('/api/jobs/' + $downloads[0].jobId)
        if ([string]$detail.summary.lifecycleState -eq 'Terminal') {
            return $detail
        }
        return $null
    }
    if (-not $downloadDone -or [string]$downloadDone.summary.terminalOutcome -ne 'Succeeded') {
        throw 'controlled_download_failed'
    }
    $result.download.passed = $true
    $result.download.terminalOutcome = 'Succeeded'
    $result.download.observedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')

    $stage = 'privilege_refresh'
    $privileges = Invoke-LocalApi -Method Get -Path '/api/v1/soulseek/privileges'
    $result.privileges.refreshed = $null -ne $privileges.refreshedAtUtc -and [int]$privileges.remainingDays -ge 0
    $result.privileges.passed = $result.privileges.refreshed
    $result.privileges.observedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    if (-not $result.privileges.passed) {
        throw 'privilege_refresh_failed'
    }

    $stage = 'await_host_completion'
    $completion = Wait-ForCondition -TimeoutSeconds (Get-BoundedTimeout ($TimeoutMinutes * 60)) -DelayMilliseconds 2000 -Condition {
        $chatState = Invoke-LocalApi -Method Get -Path '/api/v1/soulseek/chat'
        @($chatState.messages | Where-Object {
            -not $_.isOutgoing `
                -and $_.username -eq [string]$bundle.hostUsername `
                -and $_.message -eq [string]$bundle.completeMessage
        }).Count -gt 0
    }
    if (-not $completion) {
        throw 'completion_message_timeout'
    }
    $result.chat.completionReceived = $true

    $uploadDone = Wait-ForCondition -TimeoutSeconds (Get-BoundedTimeout 120) -Condition {
        $uploadState = Invoke-LocalApi -Method Get -Path '/api/v1/soulseek/uploads'
        if ([int]$uploadState.terminalCount -gt [int]$uploadBefore.terminalCount) {
            return $uploadState
        }
        return $null
    }
    if (-not $uploadDone) {
        throw 'controlled_upload_missing'
    }
    $result.upload.passed = $true
    $result.upload.completedUploadDelta = [int]$uploadDone.terminalCount - [int]$uploadBefore.terminalCount
    $result.upload.observedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    $result.chat.passed = $result.chat.replySent `
        -and $result.chat.completionReceived `
        -and $result.chat.publicToggleObserved `
        -and $result.chat.roomRefreshObserved
    $result.chat.observedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    if (-not $result.chat.passed) {
        throw 'chat_roundtrip_incomplete'
    }
}
catch {
    $result.failureStage = $stage
    $result.failureCode = 'remote_peer_failed'
}
finally {
    if ($wishlistId) {
        try {
            $null = Invoke-LocalApi -Method Delete -Path ('/api/v1/soulseek/wishlist/' + $wishlistId)
        }
        catch {
        }
    }
    try {
        $null = Invoke-LocalApi -Method Delete -Path '/api/v1/soulseek/account'
        $result.cleanup.accountDisconnected = $true
    }
    catch {
    }
    if ($process) {
        try {
            if (-not $process.HasExited) {
                $process.Kill($true)
                $process.WaitForExit(10000) | Out-Null
            }
            $result.cleanup.daemonStopped = $true
            if ($stdoutCopy) {
                $null = $stdoutCopy.GetAwaiter().GetResult()
            }
            if ($stderrCopy) {
                $null = $stderrCopy.GetAwaiter().GetResult()
            }
        }
        catch {
        }
        $process.Dispose()
    }

    try {
        if (Test-Path -LiteralPath $workRoot) {
            Remove-Item -LiteralPath $workRoot -Recurse -Force
        }
        $result.cleanup.workingDataRemoved = -not (Test-Path -LiteralPath $workRoot)
    }
    catch {
    }

    $result.completedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $EvidencePath -Encoding UTF8
    $bundle = $null
    $accessValue = $null
    $replyMessage = $null
}

if ($result.failureCode) {
    Write-Error "Controlled Soulseek peer failed during stage '$($result.failureStage)'."
    exit 1
}

Write-Host 'Controlled Soulseek peer completed successfully.'
