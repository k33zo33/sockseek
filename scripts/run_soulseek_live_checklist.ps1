param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactPath,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Reviewer,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$CandidateCommit,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{64}$')]
    [string]$ExpectedArtifactSha256,

    [ValidatePattern('^[A-Za-z0-9._-]{3,64}$')]
    [string]$RunLabel = '',

    [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')]
    [string]$Repository = 'k33zo33/sockseek',

    [ValidatePattern('^[A-Za-z0-9._-]+[.]yml$')]
    [string]$Workflow = 'soulseek-live-peer.yml',

    [ValidateRange(1, 65535)]
    [int]$ApiPort = 63002,

    [ValidateRange(1, 65535)]
    [int]$ListenPort = 49999,

    [ValidateRange(20, 35)]
    [int]$TimeoutMinutes = 30,

    [string]$OutputRoot = '',

    [switch]$PortForwardConfirmed,

    [switch]$ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$secretName = 'SOCKSEEK_LIVE_PEER_BUNDLE'
$candidate = $CandidateCommit.ToLowerInvariant()
$expectedHash = $ExpectedArtifactSha256.ToLowerInvariant()
if ([string]::IsNullOrWhiteSpace($RunLabel)) {
    $RunLabel = 'live-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
}

function New-RandomHex {
    param([Parameter(Mandatory = $true)][int]$ByteCount)

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
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
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

function Get-BoundedTimeout {
    param([Parameter(Mandatory = $true)][int]$RequestedSeconds)

    $remaining = [int][Math]::Floor(($script:runDeadline - [DateTimeOffset]::UtcNow).TotalSeconds)
    if ($remaining -le 0) {
        return 1
    }

    return [Math]::Max(1, [Math]::Min($RequestedSeconds, $remaining))
}

function Wait-ForCondition {
    param(
        [Parameter(Mandatory = $true)][scriptblock]$Condition,
        [int]$TimeoutSeconds = 120,
        [int]$DelayMilliseconds = 1000
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds((Get-BoundedTimeout $TimeoutSeconds))
    do {
        try {
            $value = & $Condition
            if ($null -ne $value -and $value -ne $false) {
                return $value
            }
        }
        catch {
            # Network startup, reconnect and not-found responses are transient here.
        }

        Start-Sleep -Milliseconds $DelayMilliseconds
    } while ([DateTimeOffset]::UtcNow -lt $deadline -and [DateTimeOffset]::UtcNow -lt $script:runDeadline)

    return $null
}

function Invoke-LocalApi {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)][string]$AccessValue,
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Path,
        [object]$Body = $null
    )

    $parameters = @{
        Uri = $BaseUrl.TrimEnd('/') + $Path
        Method = $Method
        Headers = @{ Authorization = 'Bearer ' + $AccessValue }
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

function Invoke-GitHub {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $output = & gh @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw 'github_cli_request_failed'
    }

    return ($output -join [Environment]::NewLine)
}

function Get-WorkflowRuns {
    $json = Invoke-GitHub -Arguments @(
        'run', 'list',
        '--repo', $Repository,
        '--workflow', $Workflow,
        '--limit', '30',
        '--json', 'databaseId,status,conclusion,createdAt')
    return @($json | ConvertFrom-Json)
}

function Get-WorkflowState {
    param([Parameter(Mandatory = $true)][long]$RunId)

    $json = Invoke-GitHub -Arguments @(
        'run', 'view', [string]$RunId,
        '--repo', $Repository,
        '--json', 'databaseId,status,conclusion,url')
    return $json | ConvertFrom-Json
}

function Test-ListenerPort {
    param([Parameter(Mandatory = $true)][int]$Port)

    $listeners = [Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()
    return @($listeners | Where-Object { $_.Port -eq $Port }).Count -gt 0
}

function Format-UtcSecond {
    param([Parameter(Mandatory = $true)][object]$Value)

    return ([DateTimeOffset]$Value).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
}

function Get-LatestObservation {
    param(
        [Parameter(Mandatory = $true)][object]$First,
        [Parameter(Mandatory = $true)][object]$Second
    )

    $firstValue = [DateTimeOffset]$First
    $secondValue = [DateTimeOffset]$Second
    if ($firstValue -gt $secondValue) {
        return $firstValue
    }

    return $secondValue
}

function Test-RemotePeerEvidence {
    param(
        [Parameter(Mandatory = $true)][object]$Evidence,
        [Parameter(Mandatory = $true)][string]$ExpectedCommit,
        [Parameter(Mandatory = $true)][string]$WorkflowConclusion
    )

    $remoteChecks = @(
        ([string]$Evidence.candidateCommit -eq $ExpectedCommit)
        [bool]$Evidence.exactCandidateSource
        [bool]$Evidence.connected
        [bool]$Evidence.sharing.passed
        [bool]$Evidence.readyHandshake.passed
        [bool]$Evidence.search.passed
        [bool]$Evidence.wishlist.passed
        [bool]$Evidence.folderBrowse.passed
        [bool]$Evidence.download.passed
        [bool]$Evidence.upload.passed
        [bool]$Evidence.chat.passed
        [bool]$Evidence.privileges.passed
        ($null -eq $Evidence.failureCode)
    )

    return $WorkflowConclusion -eq 'success' -and @($remoteChecks | Where-Object { -not $_ }).Count -eq 0
}

function Complete-Checklist {
    param(
        [Parameter(Mandatory = $true)][string]$ChecklistPath,
        [Parameter(Mandatory = $true)][object]$HostEvidence,
        [Parameter(Mandatory = $true)][object]$RemoteEvidence
    )

    $rows = [ordered]@{
        'Search' = @(
            (Get-LatestObservation $HostEvidence.search.observedAtUtc $RemoteEvidence.search.observedAtUtc),
            'Both controlled peers observed the other exact probe; identities and filenames omitted (host-peer.json, remote-peer.json).')
        'Wishlist' = @(
            (Get-LatestObservation $HostEvidence.wishlist.observedAtUtc $RemoteEvidence.wishlist.observedAtUtc),
            'Both on-demand wishlist searches completed with controlled results (host-peer.json, remote-peer.json).')
        'Download' = @(
            (Get-LatestObservation $HostEvidence.download.observedAtUtc $RemoteEvidence.download.observedAtUtc),
            'Both peers explicitly initiated and completed the controlled WAV transfer.')
        'Upload / sharing' = @(
            (Get-LatestObservation $HostEvidence.uploadSharing.observedAtUtc $RemoteEvidence.upload.observedAtUtc),
            'Each peer recorded a new completed upload from its single approved share root.')
        'Chat' = @(
            (Get-LatestObservation $HostEvidence.chat.observedAtUtc $RemoteEvidence.chat.observedAtUtc),
            'Bidirectional private delivery plus room refresh and public-chat toggle succeeded; content omitted.')
        'Privileges' = @(
            (Get-LatestObservation $HostEvidence.privileges.observedAtUtc $RemoteEvidence.privileges.observedAtUtc),
            'Both live clients refreshed privilege state without retaining account identity.')
        'Folder browse' = @(
            (Get-LatestObservation $HostEvidence.folderBrowse.observedAtUtc $RemoteEvidence.folderBrowse.observedAtUtc),
            'Both peers retrieved and merged the controlled remote folder.')
        'Sharing counts/status' = @(
            (Get-LatestObservation $HostEvidence.sharingCounts.observedAtUtc $RemoteEvidence.sharing.observedAtUtc),
            'Both peers advertised one approved readable root with a nonzero file count; local paths omitted.')
        'Legal-use messaging' = @(
            ([DateTimeOffset]$HostEvidence.legalUseMessaging.observedAtUtc),
            'Legal-use and user-responsibility notices were verified in the exact retained package.')
        'Provider audio invariant' = @(
            ([DateTimeOffset]$HostEvidence.providerAudioInvariant.observedAtUtc),
            'Exact-package notices and four-provider capability catalog expose no provider audio capability.')
    }

    $lines = @(Get-Content -LiteralPath $ChecklistPath)
    for ($index = 0; $index -lt $lines.Count; $index++) {
        foreach ($area in $rows.Keys) {
            if ($lines[$index] -match ('^\|\s*' + [regex]::Escape($area) + '\s*\|')) {
                $lines[$index] = '| ' + $area + ' | Pass | ' + (Format-UtcSecond $rows[$area][0]) + ' | ' + $rows[$area][1] + ' |'
            }
        }
    }

    $lines = $lines | ForEach-Object {
        $_ -replace '^- Start time UTC: TODO$', ('- Start time UTC: ' + (Format-UtcSecond $HostEvidence.startedAtUtc)) `
           -replace '^- End time UTC: TODO$', ('- End time UTC: ' + (Format-UtcSecond $HostEvidence.completedAtUtc)) `
           -replace '^- Diagnostics export reviewed and redacted: TODO$', '- Diagnostics export reviewed and redacted: Structured count/boolean evidence was reviewed; no diagnostics export, identities or message content retained.' `
           -replace '^- Automated gate transcript or exact reference: TODO$', '- Automated gate transcript or exact reference: evidence/host-peer.json, evidence/remote-peer.json and scripts/validate_soulseek_live_checklist.ps1.' `
           -replace '^- Failures, retries or deviations: TODO$', '- Failures, retries or deviations: Ready-handshake sends were bounded and idempotent; the accepted run completed without an evidence exception.'
    }
    Set-Content -LiteralPath $ChecklistPath -Value $lines -Encoding UTF8
}

if ($ValidateOnly) {
    if (-not $PortForwardConfirmed) {
        throw 'Validation requires the explicit port-forward confirmation switch.'
    }
    $validationTimestamp = [DateTimeOffset]::UtcNow.ToString('o')
    $validationRemote = [pscustomobject]@{
        candidateCommit = $candidate
        exactCandidateSource = $true
        connected = $true
        sharing = [pscustomobject]@{ passed = $true; observedAtUtc = $validationTimestamp }
        readyHandshake = [pscustomobject]@{ passed = $true }
        search = [pscustomobject]@{ passed = $true; observedAtUtc = $validationTimestamp }
        wishlist = [pscustomobject]@{ passed = $true; observedAtUtc = $validationTimestamp }
        folderBrowse = [pscustomobject]@{ passed = $true; observedAtUtc = $validationTimestamp }
        download = [pscustomobject]@{ passed = $true; observedAtUtc = $validationTimestamp }
        upload = [pscustomobject]@{ passed = $true; observedAtUtc = $validationTimestamp }
        chat = [pscustomobject]@{ passed = $true; observedAtUtc = $validationTimestamp }
        privileges = [pscustomobject]@{ passed = $true; observedAtUtc = $validationTimestamp }
        failureCode = $null
    }
    if (-not (Test-RemotePeerEvidence -Evidence $validationRemote -ExpectedCommit $candidate -WorkflowConclusion 'success')) {
        throw 'Validation rejected complete remote evidence.'
    }
    $validationRemote.search.passed = $false
    if (Test-RemotePeerEvidence -Evidence $validationRemote -ExpectedCommit $candidate -WorkflowConclusion 'success') {
        throw 'Validation accepted incomplete remote evidence.'
    }
    $validationRemote.search.passed = $true
    $validationHost = [pscustomobject]@{
        startedAtUtc = $validationTimestamp
        completedAtUtc = $validationTimestamp
        search = [pscustomobject]@{ observedAtUtc = $validationTimestamp }
        wishlist = [pscustomobject]@{ observedAtUtc = $validationTimestamp }
        download = [pscustomobject]@{ observedAtUtc = $validationTimestamp }
        uploadSharing = [pscustomobject]@{ observedAtUtc = $validationTimestamp }
        chat = [pscustomobject]@{ observedAtUtc = $validationTimestamp }
        privileges = [pscustomobject]@{ observedAtUtc = $validationTimestamp }
        folderBrowse = [pscustomobject]@{ observedAtUtc = $validationTimestamp }
        sharingCounts = [pscustomobject]@{ observedAtUtc = $validationTimestamp }
        legalUseMessaging = [pscustomobject]@{ observedAtUtc = $validationTimestamp }
        providerAudioInvariant = [pscustomobject]@{ observedAtUtc = $validationTimestamp }
    }
    $validationChecklist = [IO.Path]::GetTempFileName()
    try {
        @(
            '| Search | Pending | TODO | TODO |'
            '| Wishlist | Pending | TODO | TODO |'
            '| Download | Pending | TODO | TODO |'
            '| Upload / sharing | Pending | TODO | TODO |'
            '| Chat | Pending | TODO | TODO |'
            '| Privileges | Pending | TODO | TODO |'
            '| Folder browse | Pending | TODO | TODO |'
            '| Sharing counts/status | Pending | TODO | TODO |'
            '| Legal-use messaging | Pending | TODO | TODO |'
            '| Provider audio invariant | Pending | TODO | TODO |'
            '- Start time UTC: TODO'
            '- End time UTC: TODO'
            '- Diagnostics export reviewed and redacted: TODO'
            '- Automated gate transcript or exact reference: TODO'
            '- Failures, retries or deviations: TODO'
        ) | Set-Content -LiteralPath $validationChecklist -Encoding UTF8
        Complete-Checklist -ChecklistPath $validationChecklist -HostEvidence $validationHost -RemoteEvidence $validationRemote
        $completedValidationChecklist = Get-Content -LiteralPath $validationChecklist -Raw
        if ($completedValidationChecklist -match '(?i)\bTODO\b' -or @($completedValidationChecklist -split "`r?`n" | Where-Object { $_ -match '^\|.+\| Pass \|' }).Count -ne 10) {
            throw 'Validation could not complete every checklist row and note.'
        }
    }
    finally {
        Remove-Item -LiteralPath $validationChecklist -Force -ErrorAction SilentlyContinue
    }
    Write-Host 'Controlled Soulseek live-checklist orchestration validation passed.'
    exit 0
}

if (-not $PortForwardConfirmed) {
    throw 'Refusing to start: confirm the temporary TCP port forward with -PortForwardConfirmed.'
}
if (-not [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Windows)) {
    throw 'The retained Windows artifact live-checklist host must run on Windows.'
}
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'GitHub CLI is required for the controlled remote peer.'
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$resolvedArtifact = (Resolve-Path -LiteralPath $ArtifactPath -ErrorAction Stop).Path
$actualHash = (Get-FileHash -LiteralPath $resolvedArtifact -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -ne $expectedHash) {
    throw 'The retained artifact SHA256 does not match the explicitly approved hash.'
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot '.tmp\soulseek-live-checklists'
}
elseif (-not [IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot $OutputRoot
}

$existingSecrets = @(Invoke-GitHub -Arguments @('secret', 'list', '--repo', $Repository, '--json', 'name') | ConvertFrom-Json)
if (@($existingSecrets | Where-Object { $_.name -eq $secretName }).Count -gt 0) {
    throw 'The temporary controlled-peer repository secret already exists; remove or investigate it before retrying.'
}
$runsBefore = @(Get-WorkflowRuns)
if (@($runsBefore | Where-Object { $_.status -in @('queued', 'in_progress', 'waiting', 'requested', 'pending') }).Count -gt 0) {
    throw 'A controlled-peer workflow is already active; wait for it to finish before starting another.'
}
foreach ($requiredFreePort in @($ApiPort, $ListenPort)) {
    if (Test-ListenerPort -Port $requiredFreePort) {
        throw "Required local port $requiredFreePort is already in use."
    }
}
$runIdsBefore = @($runsBefore | ForEach-Object { [long]$_.databaseId })

$checklistsBefore = @(Get-ChildItem -LiteralPath $OutputRoot -Directory -ErrorAction SilentlyContinue)
& (Join-Path $repoRoot 'scripts\new_soulseek_live_checklist.ps1') `
    -ArtifactPath $resolvedArtifact `
    -Reviewer $Reviewer `
    -SessionLabel $RunLabel `
    -ExpectedCommit $candidate `
    -RequiredHost Any `
    -OutputRoot $OutputRoot | Out-Null
$beforePaths = @($checklistsBefore | ForEach-Object FullName)
$runDirectory = Get-ChildItem -LiteralPath $OutputRoot -Directory |
    Where-Object { $_.FullName -notin $beforePaths } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
if (-not $runDirectory) {
    throw 'The live-checklist scaffold could not be identified.'
}

$startedAt = [DateTimeOffset]::UtcNow
$script:runDeadline = $startedAt.AddMinutes($TimeoutMinutes)
$evidenceDirectory = Join-Path $runDirectory.FullName 'evidence'
$hostEvidencePath = Join-Path $evidenceDirectory 'host-peer.json'
$remoteEvidencePath = Join-Path $evidenceDirectory 'remote-peer.json'
$downloadedRemoteEvidencePath = Join-Path $evidenceDirectory 'soulseek-live-peer-evidence.json'
$checklistPath = Join-Path $runDirectory.FullName 'checklist.md'
$temporaryBoundary = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$workRoot = [IO.Path]::GetFullPath((Join-Path $temporaryBoundary ('sockseek-live-host-' + $RunLabel)))
$boundaryPrefix = $temporaryBoundary.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $workRoot.StartsWith($boundaryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The bounded live-checklist working directory escaped the temporary directory.'
}
$packageRoot = Join-Path $workRoot 'package'
$configRoot = Join-Path $workRoot 'config'
$shareRoot = Join-Path $workRoot 'share'
$downloadRoot = Join-Path $workRoot 'downloads'
$baseUrl = "http://127.0.0.1:$ApiPort"
$process = $null
$stdoutCopy = $null
$stderrCopy = $null
$accessValue = $null
$hostUsername = $null
$hostPassword = $null
$remoteUsername = $null
$remotePassword = $null
$hostProbe = $null
$remoteProbe = $null
$readyMessage = $null
$replyMessage = $null
$completeMessage = $null
$bundleJson = $null
$wishlistId = $null
$workflowRunId = $null
$secretCreated = $false
$dispatchAttempted = $false
$stage = 'initialize'
$remoteEvidence = $null
$result = [ordered]@{
    schemaVersion = 1
    runLabel = $RunLabel
    candidateCommit = $candidate
    artifactSha256 = $actualHash
    startedAtUtc = $startedAt.ToString('o')
    completedAtUtc = $null
    exactPackage = $true
    connected = $false
    listener = [ordered]@{ passed = $false; port = $ListenPort }
    sharingCounts = [ordered]@{ passed = $false; observedAtUtc = $null; directoryCount = 0; fileCount = 0; includedRootCount = 0 }
    readyHandshake = [ordered]@{ passed = $false; observedAtUtc = $null }
    search = [ordered]@{ passed = $false; observedAtUtc = $null; controlledResultCount = 0 }
    wishlist = [ordered]@{ passed = $false; observedAtUtc = $null; controlledResultCount = 0 }
    folderBrowse = [ordered]@{ passed = $false; observedAtUtc = $null; fullyRetrieved = $false }
    download = [ordered]@{ passed = $false; observedAtUtc = $null; terminalOutcome = $null }
    uploadSharing = [ordered]@{ passed = $false; observedAtUtc = $null; completedUploadDelta = 0 }
    chat = [ordered]@{ passed = $false; observedAtUtc = $null; privateDirectionsObserved = 0; publicToggleObserved = $false; roomRefreshObserved = $false }
    privileges = [ordered]@{ passed = $false; observedAtUtc = $null; refreshed = $false }
    legalUseMessaging = [ordered]@{ passed = $false; observedAtUtc = $null; packagedNoticePresent = $false }
    providerAudioInvariant = [ordered]@{ passed = $false; observedAtUtc = $null; providerCount = 0; forbiddenCapabilityCount = 0; packagedNoticePresent = $false }
    remote = [ordered]@{ workflowRunId = $null; evidenceVerified = $false; cleanupVerified = $false }
    cleanup = [ordered]@{ accountDisconnected = $false; daemonStopped = $false; workingDataRemoved = $false; repositoryControlRemoved = $false; failedWorkflowCancelled = $false }
    failureStage = $null
    failureCode = $null
}

try {
    $stage = 'prepare_exact_package'
    if (Test-Path -LiteralPath $workRoot) {
        throw 'The generated bounded working directory already exists.'
    }
    New-Item -ItemType Directory -Path $packageRoot, $configRoot, $shareRoot, $downloadRoot -Force | Out-Null
    Expand-Archive -LiteralPath $resolvedArtifact -DestinationPath $packageRoot
    $releaseMetadata = Get-Content -LiteralPath (Join-Path $packageRoot 'release-metadata.json') -Raw | ConvertFrom-Json
    if ([string]$releaseMetadata.commit -ne $candidate -or [string]$releaseMetadata.license -ne 'AGPL-3.0') {
        throw 'The extracted package metadata does not match the approved candidate and license.'
    }
    $serverPath = Join-Path $packageRoot 'daemon\Sockseek.Server.exe'
    if (-not (Test-Path -LiteralPath $serverPath -PathType Leaf)) {
        throw 'The exact package does not contain the expected daemon executable.'
    }

    $accessValue = New-RandomHex -ByteCount 32
    $hostUsername = 'ssrc' + (New-RandomHex -ByteCount 12)
    $hostPassword = New-RandomHex -ByteCount 24
    $remoteUsername = 'ssrc' + (New-RandomHex -ByteCount 12)
    $remotePassword = New-RandomHex -ByteCount 24
    $hostProbe = 'sockseek-host-' + (New-RandomHex -ByteCount 10)
    $remoteProbe = 'sockseek-remote-' + (New-RandomHex -ByteCount 10)
    $readyMessage = 'ready-' + (New-RandomHex -ByteCount 16)
    $replyMessage = 'reply-' + (New-RandomHex -ByteCount 16)
    $completeMessage = 'complete-' + (New-RandomHex -ByteCount 16)
    New-ProbeWaveFile -Path (Join-Path $shareRoot ($hostProbe + '.wav'))

    $stage = 'start_exact_package_daemon'
    $startInfo = New-Object Diagnostics.ProcessStartInfo
    $startInfo.FileName = $serverPath
    $startInfo.Arguments = '--urls ' + $baseUrl
    $startInfo.WorkingDirectory = Split-Path -Parent $serverPath
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.EnvironmentVariables['SockseekServer__ConfigDir'] = $configRoot
    $startInfo.EnvironmentVariables['SockseekServer__SessionToken'] = $accessValue
    $startInfo.EnvironmentVariables['SockseekServer__Engine__ListenPort'] = [string]$ListenPort
    $startInfo.EnvironmentVariables['SockseekServer__DefaultDownload__Output__ParentDir'] = $downloadRoot
    $startInfo.EnvironmentVariables['SOCKSEEK_DESKTOP_HANDSHAKE_STDOUT'] = '0'
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw 'daemon_start_failed'
    }
    $stdoutCopy = $process.StandardOutput.ReadToEndAsync()
    $stderrCopy = $process.StandardError.ReadToEndAsync()

    $healthy = Wait-ForCondition -TimeoutSeconds 90 -Condition {
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

    $stage = 'configure_host_peer'
    $null = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Put -Path '/api/v1/soulseek/account' -Body @{
        username = $hostUsername
        password = $hostPassword
    }
    $hostPassword = $null
    $sharing = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Post -Path '/api/v1/soulseek/sharing/roots' -Body @{
        path = $shareRoot
        virtualName = 'LiveEvidence'
        enabled = $true
    }
    $includedRoots = @($sharing.roots | Where-Object { $_.included })
    $result.sharingCounts.directoryCount = [int]$sharing.directoryCount
    $result.sharingCounts.fileCount = [int]$sharing.fileCount
    $result.sharingCounts.includedRootCount = $includedRoots.Count
    $result.sharingCounts.passed = [int]$sharing.fileCount -ge 1 -and $includedRoots.Count -eq 1
    $result.sharingCounts.observedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    if (-not $result.sharingCounts.passed) {
        throw 'host_share_snapshot_invalid'
    }

    $null = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Post -Path '/api/jobs/search' -Body @{ queryText = 'controlled peer connect test' }
    $connected = Wait-ForCondition -TimeoutSeconds 120 -DelayMilliseconds 2000 -Condition {
        $state = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Get -Path '/api/v1/soulseek/account'
        return $state.isConnected
    }
    if (-not $connected) {
        throw 'host_soulseek_connection_timeout'
    }
    $result.connected = $true
    $result.listener.passed = [bool](Wait-ForCondition -TimeoutSeconds 30 -Condition { Test-ListenerPort -Port $ListenPort })
    if (-not $result.listener.passed) {
        throw 'host_listener_not_observed'
    }
    $uploadBefore = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Get -Path '/api/v1/soulseek/uploads'

    $stage = 'verify_package_policy'
    $instructions = Get-Content -LiteralPath (Join-Path $packageRoot 'closed-beta-tester-instructions.md') -Raw
    $limitations = Get-Content -LiteralPath (Join-Path $packageRoot 'docs\beta-limitations.md') -Raw
    $legalNotice = $instructions -match '(?i)legally allowed|applicable law' -and $limitations -match '(?i)legal-use notice|legally allowed'
    $providerNotice = $instructions -match '(?i)metadata|playlist' -and $instructions -match '(?i)not.*audio|never.*audio' -and $limitations -match '(?i)never audio sources'
    $providersResponse = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Get -Path '/api/v1/providers'
    $providers = @($providersResponse | ForEach-Object { $_ })
    $forbiddenCapabilities = @($providers | Where-Object {
        (($_.capabilities -join ' ') + ' ' + $_.PSObject.Properties.Name) -match '(?i)playback|audio.?stream|audio.?url|download.?track'
    })
    $result.legalUseMessaging.packagedNoticePresent = [bool]$legalNotice
    $result.legalUseMessaging.passed = [bool]$legalNotice
    $result.legalUseMessaging.observedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    $result.providerAudioInvariant.providerCount = $providers.Count
    $result.providerAudioInvariant.forbiddenCapabilityCount = $forbiddenCapabilities.Count
    $result.providerAudioInvariant.packagedNoticePresent = [bool]$providerNotice
    $result.providerAudioInvariant.passed = $providerNotice -and $providers.Count -eq 4 -and $forbiddenCapabilities.Count -eq 0
    $result.providerAudioInvariant.observedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    if (-not $result.legalUseMessaging.passed -or -not $result.providerAudioInvariant.passed) {
        throw 'package_policy_failed'
    }

    $stage = 'create_temporary_remote_secret'
    $bundleJson = [ordered]@{
        remoteUsername = $remoteUsername
        remotePassword = $remotePassword
        hostUsername = $hostUsername
        remoteProbe = $remoteProbe
        hostProbe = $hostProbe
        readyMessage = $readyMessage
        replyMessage = $replyMessage
        completeMessage = $completeMessage
    } | ConvertTo-Json -Compress
    $secretCreated = $true
    $secretOutput = $bundleJson | & gh secret set $secretName --repo $Repository 2>&1
    $bundleJson = $null
    $remotePassword = $null
    if ($LASTEXITCODE -ne 0) {
        $secretOutput = $null
        throw 'temporary_repository_secret_create_failed'
    }
    $secretOutput = $null

    $stage = 'dispatch_remote_peer'
    $dispatchAttempted = $true
    $null = Invoke-GitHub -Arguments @(
        'workflow', 'run', $Workflow,
        '--repo', $Repository,
        '-f', ('candidate_commit=' + $candidate),
        '-f', ('run_label=' + $RunLabel))
    $newRun = Wait-ForCondition -TimeoutSeconds 90 -DelayMilliseconds 3000 -Condition {
        @(Get-WorkflowRuns | Where-Object { [long]$_.databaseId -notin $runIdsBefore }) |
            Sort-Object createdAt -Descending |
            Select-Object -First 1
    }
    if (-not $newRun) {
        throw 'remote_workflow_run_not_found'
    }
    $workflowRunId = [long]$newRun.databaseId
    $result.remote.workflowRunId = $workflowRunId

    $stage = 'ready_handshake'
    $handshakeState = @{ lastReadySend = [DateTimeOffset]::MinValue }
    $replyObserved = Wait-ForCondition -TimeoutSeconds 600 -DelayMilliseconds 3000 -Condition {
        if (([DateTimeOffset]::UtcNow - $handshakeState.lastReadySend).TotalSeconds -ge 15) {
            $null = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Post -Path '/api/v1/soulseek/chat/private/messages' -Body @{
                username = $remoteUsername
                message = $readyMessage
            }
            $handshakeState.lastReadySend = [DateTimeOffset]::UtcNow
        }
        $chatState = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Get -Path '/api/v1/soulseek/chat'
        return @($chatState.messages | Where-Object {
            -not $_.isOutgoing -and $_.username -eq $remoteUsername -and $_.message -eq $replyMessage
        }).Count -gt 0
    }
    if (-not $replyObserved) {
        throw 'remote_ready_reply_timeout'
    }
    $result.readyHandshake.passed = $true
    $result.readyHandshake.observedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    $result.chat.privateDirectionsObserved = 2

    $stage = 'host_controlled_search'
    $searchJob = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Post -Path '/api/jobs/search' -Body @{ queryText = $remoteProbe }
    $searchSnapshot = Wait-ForCondition -TimeoutSeconds 180 -DelayMilliseconds 1500 -Condition {
        $snapshot = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Get -Path ('/api/jobs/' + $searchJob.jobId + '/results/files')
        $controlled = @($snapshot.items | Where-Object {
            $_.username -eq $remoteUsername -and $_.filename -like ('*' + $remoteProbe + '*')
        })
        if ($controlled.Count -gt 0) {
            return [pscustomobject]@{ candidates = $controlled }
        }
        return $null
    }
    if (-not $searchSnapshot) {
        throw 'host_controlled_search_timeout'
    }
    $remoteFile = @($searchSnapshot.candidates)[0]
    $result.search.passed = $true
    $result.search.controlledResultCount = @($searchSnapshot.candidates).Count
    $result.search.observedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')

    $stage = 'host_controlled_wishlist'
    $wishlist = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Post -Path '/api/v1/soulseek/wishlist' -Body @{
        query = $remoteProbe
        intervalMinutes = 30
        enabled = $true
    }
    $wishlistId = [string]$wishlist.id
    $null = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Post -Path ('/api/v1/soulseek/wishlist/' + $wishlistId + '/run')
    $wishlistDone = Wait-ForCondition -TimeoutSeconds 180 -DelayMilliseconds 1500 -Condition {
        $entriesResponse = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Get -Path '/api/v1/soulseek/wishlist'
        $entry = @($entriesResponse | ForEach-Object { $_ }) |
            Where-Object { [string]$_.id -eq $wishlistId } |
            Select-Object -First 1
        $completedProperty = if ($entry) { $entry.PSObject.Properties['lastSearchCompletedAtUtc'] } else { $null }
        if ($completedProperty -and $null -ne $completedProperty.Value) {
            return $entry
        }
        return $null
    }
    $wishlistCountProperty = if ($wishlistDone) { $wishlistDone.PSObject.Properties['lastResultCount'] } else { $null }
    $wishlistCount = if ($wishlistCountProperty) { [int]$wishlistCountProperty.Value } else { 0 }
    if (-not $wishlistDone -or $wishlistCount -lt 1) {
        throw 'host_controlled_wishlist_timeout'
    }
    $result.wishlist.passed = $true
    $result.wishlist.controlledResultCount = $wishlistCount
    $result.wishlist.observedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')

    $stage = 'host_controlled_folder_browse'
    $folders = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Get -Path ('/api/jobs/' + $searchJob.jobId + '/results/folders?includeFiles=true')
    $folder = @($folders.items | Where-Object { $_.username -eq $remoteUsername }) | Select-Object -First 1
    if (-not $folder) {
        throw 'host_controlled_folder_missing'
    }
    $retrieveJob = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Post -Path ('/api/jobs/' + $searchJob.jobId + '/retrieve-folder') -Body @{
        folder = @{ username = $remoteUsername; folderPath = [string]$folder.folderPath }
    }
    $retrieveDone = Wait-ForCondition -TimeoutSeconds 120 -Condition {
        $detail = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Get -Path ('/api/jobs/' + $retrieveJob.jobId)
        if ([string]$detail.summary.lifecycleState -eq 'Terminal') {
            return $detail
        }
        return $null
    }
    if (-not $retrieveDone -or [string]$retrieveDone.summary.terminalOutcome -ne 'Succeeded') {
        throw 'host_controlled_folder_retrieval_failed'
    }
    $foldersAfter = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Get -Path ('/api/jobs/' + $searchJob.jobId + '/results/folders?includeFiles=true')
    $retrieved = @($foldersAfter.items | Where-Object { $_.username -eq $remoteUsername -and $_.isFullyRetrieved }) | Select-Object -First 1
    if (-not $retrieved) {
        throw 'host_controlled_folder_not_merged'
    }
    $result.folderBrowse.passed = $true
    $result.folderBrowse.fullyRetrieved = $true
    $result.folderBrowse.observedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')

    $stage = 'host_controlled_download'
    $downloadResponse = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Post -Path ('/api/jobs/' + $searchJob.jobId + '/downloads/files') -Body @{
        files = @(@{ username = [string]$remoteFile.ref.username; filename = [string]$remoteFile.ref.filename })
    }
    $downloads = @($downloadResponse | ForEach-Object { $_ })
    if ($downloads.Count -ne 1) {
        throw 'host_controlled_download_not_created'
    }
    $downloadDone = Wait-ForCondition -TimeoutSeconds 180 -Condition {
        $detail = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Get -Path ('/api/jobs/' + $downloads[0].jobId)
        if ([string]$detail.summary.lifecycleState -eq 'Terminal') {
            return $detail
        }
        return $null
    }
    if (-not $downloadDone -or [string]$downloadDone.summary.terminalOutcome -ne 'Succeeded') {
        throw 'host_controlled_download_failed'
    }
    $result.download.passed = $true
    $result.download.terminalOutcome = 'Succeeded'
    $result.download.observedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')

    $stage = 'host_chat_and_privileges'
    $rooms = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Post -Path '/api/v1/soulseek/chat/rooms/refresh'
    $publicStarted = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Post -Path '/api/v1/soulseek/chat/public/start'
    $publicStopped = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Post -Path '/api/v1/soulseek/chat/public/stop'
    $result.chat.roomRefreshObserved = $null -ne $rooms.availableRooms
    $result.chat.publicToggleObserved = $publicStarted.publicChatEnabled -and -not $publicStopped.publicChatEnabled
    $result.chat.passed = $result.chat.privateDirectionsObserved -eq 2 -and $result.chat.roomRefreshObserved -and $result.chat.publicToggleObserved
    $result.chat.observedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    if (-not $result.chat.passed) {
        throw 'host_chat_actions_failed'
    }
    $privileges = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Get -Path '/api/v1/soulseek/privileges'
    $result.privileges.refreshed = $null -ne $privileges.refreshedAtUtc -and [int]$privileges.remainingDays -ge 0
    $result.privileges.passed = $result.privileges.refreshed
    $result.privileges.observedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    if (-not $result.privileges.passed) {
        throw 'host_privilege_refresh_failed'
    }

    $stage = 'host_controlled_upload'
    $hostUploadDone = Wait-ForCondition -TimeoutSeconds 180 -Condition {
        $uploadState = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Get -Path '/api/v1/soulseek/uploads'
        if ([int]$uploadState.terminalCount -gt [int]$uploadBefore.terminalCount) {
            return $uploadState
        }
        return $null
    }
    if (-not $hostUploadDone) {
        throw 'host_controlled_upload_missing'
    }
    $result.uploadSharing.passed = $true
    $result.uploadSharing.completedUploadDelta = [int]$hostUploadDone.terminalCount - [int]$uploadBefore.terminalCount
    $result.uploadSharing.observedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')

    $stage = 'completion_handshake'
    $null = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Post -Path '/api/v1/soulseek/chat/private/messages' -Body @{
        username = $remoteUsername
        message = $completeMessage
    }

    $stage = 'await_remote_workflow'
    $terminalRun = Wait-ForCondition -TimeoutSeconds (Get-BoundedTimeout ($TimeoutMinutes * 60)) -DelayMilliseconds 5000 -Condition {
        $state = Get-WorkflowState -RunId $workflowRunId
        if ($state.status -eq 'completed') {
            return $state
        }
        return $null
    }
    if (-not $terminalRun) {
        throw 'remote_workflow_timeout'
    }

    $stage = 'download_remote_evidence'
    $artifactName = 'soulseek-live-peer-' + $RunLabel
    $null = Invoke-GitHub -Arguments @(
        'run', 'download', [string]$workflowRunId,
        '--repo', $Repository,
        '--name', $artifactName,
        '--dir', $evidenceDirectory)
    if (-not (Test-Path -LiteralPath $downloadedRemoteEvidencePath -PathType Leaf)) {
        throw 'remote_evidence_missing'
    }
    Move-Item -LiteralPath $downloadedRemoteEvidencePath -Destination $remoteEvidencePath
    $remoteEvidence = Get-Content -LiteralPath $remoteEvidencePath -Raw | ConvertFrom-Json
    if (-not (Test-RemotePeerEvidence -Evidence $remoteEvidence -ExpectedCommit $candidate -WorkflowConclusion ([string]$terminalRun.conclusion))) {
        throw 'remote_evidence_failed'
    }
    $result.remote.evidenceVerified = $true
    $result.remote.cleanupVerified = [bool]$remoteEvidence.cleanup.accountDisconnected `
        -and [bool]$remoteEvidence.cleanup.daemonStopped `
        -and [bool]$remoteEvidence.cleanup.workingDataRemoved
    if (-not $result.remote.cleanupVerified) {
        throw 'remote_cleanup_evidence_failed'
    }
}
catch {
    $result.failureStage = $stage
    $result.failureCode = 'live_checklist_orchestration_failed'
}
finally {
    if ($wishlistId -and $accessValue) {
        try {
            $null = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Delete -Path ('/api/v1/soulseek/wishlist/' + $wishlistId)
        }
        catch {
        }
    }
    if ($accessValue) {
        try {
            $null = Invoke-LocalApi -BaseUrl $baseUrl -AccessValue $accessValue -Method Delete -Path '/api/v1/soulseek/account'
            $result.cleanup.accountDisconnected = $true
        }
        catch {
        }
    }
    if (-not $workflowRunId -and $dispatchAttempted) {
        try {
            $dispatchedRun = @(Get-WorkflowRuns | Where-Object { [long]$_.databaseId -notin $runIdsBefore }) |
                Sort-Object createdAt -Descending |
                Select-Object -First 1
            if ($dispatchedRun) {
                $workflowRunId = [long]$dispatchedRun.databaseId
                $result.remote.workflowRunId = $workflowRunId
            }
        }
        catch {
        }
    }
    if ($workflowRunId -and $result.failureCode) {
        try {
            $state = Get-WorkflowState -RunId $workflowRunId
            if ($state.status -ne 'completed') {
                $null = Invoke-GitHub -Arguments @('run', 'cancel', [string]$workflowRunId, '--repo', $Repository)
                $result.cleanup.failedWorkflowCancelled = $true
            }
        }
        catch {
        }
    }
    if ($secretCreated) {
        for ($cleanupAttempt = 0; $cleanupAttempt -lt 3 -and -not $result.cleanup.repositoryControlRemoved; $cleanupAttempt++) {
            try {
                $remainingSecrets = @(Invoke-GitHub -Arguments @('secret', 'list', '--repo', $Repository, '--json', 'name') | ConvertFrom-Json)
                if (@($remainingSecrets | Where-Object { $_.name -eq $secretName }).Count -eq 0) {
                    $result.cleanup.repositoryControlRemoved = $true
                    break
                }
                $null = Invoke-GitHub -Arguments @('secret', 'delete', $secretName, '--repo', $Repository)
            }
            catch {
            }
            if (-not $result.cleanup.repositoryControlRemoved) {
                Start-Sleep -Seconds 2
            }
        }
    }
    if ($process) {
        try {
            if (-not $process.HasExited) {
                $process.Kill()
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
    $result | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath $hostEvidencePath -Encoding UTF8
    $accessValue = $null
    $hostUsername = $null
    $hostPassword = $null
    $remoteUsername = $null
    $remotePassword = $null
    $hostProbe = $null
    $remoteProbe = $null
    $readyMessage = $null
    $replyMessage = $null
    $completeMessage = $null
    $bundleJson = $null
}

$requiredCleanup = $result.cleanup.accountDisconnected `
    -and $result.cleanup.daemonStopped `
    -and $result.cleanup.workingDataRemoved `
    -and $result.cleanup.repositoryControlRemoved
if (-not $requiredCleanup -and -not $result.failureCode) {
    $result.failureStage = 'cleanup'
    $result.failureCode = 'live_checklist_cleanup_failed'
    $result | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath $hostEvidencePath -Encoding UTF8
}

if (-not $result.failureCode) {
    try {
        Complete-Checklist -ChecklistPath $checklistPath -HostEvidence $result -RemoteEvidence $remoteEvidence
        & (Join-Path $repoRoot 'scripts\validate_soulseek_live_checklist.ps1') `
            -ArtifactPath $resolvedArtifact `
            -RunDirectory $runDirectory.FullName | Out-Null
    }
    catch {
        $result.failureStage = 'finalize_checklist'
        $result.failureCode = 'live_checklist_finalization_failed'
        $result | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath $hostEvidencePath -Encoding UTF8
    }
}

if ($result.failureCode) {
    Write-Error "Controlled Soulseek live checklist failed during stage '$($result.failureStage)'."
    exit 1
}

Write-Host 'Controlled Soulseek live checklist completed and validated.'
Write-Host "Run directory: $($runDirectory.FullName)"
Write-Host "Workflow run: $workflowRunId"
