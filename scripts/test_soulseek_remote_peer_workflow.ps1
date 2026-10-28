param(
    [string]$Root = '.'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$rootPath = (Resolve-Path -LiteralPath $Root).Path
$workflowPath = Join-Path $rootPath '.github/workflows/soulseek-live-peer.yml'
$runnerPath = Join-Path $rootPath 'scripts/run_soulseek_remote_peer.ps1'
foreach ($path in @($workflowPath, $runnerPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required controlled-peer file is missing: $path"
    }
}

$workflow = Get-Content -LiteralPath $workflowPath -Raw
$runner = Get-Content -LiteralPath $runnerPath -Raw

function Assert-Contains {
    param([string]$Text, [string]$Value, [string]$Description)
    if ($Text.IndexOf($Value, [StringComparison]::Ordinal) -lt 0) {
        throw "Controlled-peer workflow is missing $Description."
    }
}

Assert-Contains $workflow 'workflow_dispatch:' 'the manual-only trigger'
Assert-Contains $workflow 'permissions:' 'an explicit permissions block'
Assert-Contains $workflow 'contents: read' 'read-only repository permissions'
Assert-Contains $workflow 'timeout-minutes: 25' 'the bounded job timeout'
Assert-Contains $workflow '${{ secrets.SOCKSEEK_LIVE_PEER_BUNDLE }}' 'the encrypted control bundle reference'
Assert-Contains $workflow 'ref: ${{ inputs.candidate_commit }}' 'the exact candidate checkout input'
Assert-Contains $workflow "-CandidateCommit '`${{ inputs.candidate_commit }}'" 'the evidence commit binding'
Assert-Contains $workflow "CANDIDATE_COMMIT -notmatch '^[0-9a-fA-F]{40}$'" 'full commit validation before candidate checkout'
Assert-Contains $workflow './scripts/run_soulseek_remote_peer.ps1' 'the redacting peer runner'
Assert-Contains $workflow 'if: ${{ always()' 'failure-safe evidence upload'
Assert-Contains $workflow 'retention-days: 7' 'bounded evidence retention'

if ($workflow -match '(?m)^\s{2}(push|pull_request|schedule):') {
    throw 'Controlled-peer workflow must remain manual-only.'
}
if ($workflow -match '(?m)^\s*(write-all|actions:\s*write|contents:\s*write)\s*$') {
    throw 'Controlled-peer workflow requests write permissions.'
}

Assert-Contains $runner "[Environment]::GetEnvironmentVariable('SOCKSEEK_LIVE_PEER_BUNDLE')" 'environment-only bundle ingestion'
Assert-Contains $runner "[Environment]::SetEnvironmentVariable('SOCKSEEK_LIVE_PEER_BUNDLE', `$null)" 'bundle removal before daemon launch'
Assert-Contains $runner "failureCode = 'remote_peer_failed'" 'a stable redacted failure code'
Assert-Contains $runner 'Remove-Item -LiteralPath $workRoot -Recurse -Force' 'bounded working-data cleanup'
Assert-Contains $runner 'escaped the temporary directory' 'an explicit temporary-directory cleanup boundary check'
Assert-Contains $runner "-Path '/api/v1/soulseek/account'" 'explicit account cleanup'
Assert-Contains $runner 'Get-BoundedTimeout' 'a global live-run deadline'
Assert-Contains $runner "'replyMessage'" 'an exact bidirectional chat reply field'
Assert-Contains $runner 'message = [string]$bundle.replyMessage' 'the exact host-verifiable chat reply'

if ($runner -match '(?im)Write-(Host|Output|Verbose|Debug|Information|Warning)\s+.*(?:bundle|remoteUsername|remotePassword|hostUsername|readyMessage|completeMessage)') {
    throw 'Controlled-peer runner may not print bundle fields.'
}
if ($runner -match '(?im)ConvertTo-Json.*(?:bundle|remoteUsername|remotePassword|hostUsername|readyMessage|completeMessage)') {
    throw 'Controlled-peer runner may not serialize bundle fields into evidence.'
}

$fakeValues = [ordered]@{
    remoteUsername = 'remote_validation_user'
    remotePassword = 'remote_validation_password_9f4a'
    hostUsername = 'host_validation_user'
    remoteProbe = 'remote-validation-probe-9f4a'
    hostProbe = 'host-validation-probe-9f4a'
    readyMessage = 'ready-validation-message-9f4a'
    replyMessage = 'reply-validation-message-9f4a'
    completeMessage = 'complete-validation-message-9f4a'
}
$previousBundle = [Environment]::GetEnvironmentVariable('SOCKSEEK_LIVE_PEER_BUNDLE')
try {
    [Environment]::SetEnvironmentVariable(
        'SOCKSEEK_LIVE_PEER_BUNDLE',
        ($fakeValues | ConvertTo-Json -Compress))
    $hostExecutable = (Get-Process -Id $PID).Path
    $output = & $hostExecutable `
        -NoProfile `
        -File $runnerPath `
        -PublishedServerPath 'validation-only' `
        -RunLabel 'validation-run' `
        -CandidateCommit 'c7b4cc5ba18cc3ce4f9c2b3fa83734b07d67ac0f' `
        -EvidencePath (Join-Path ([IO.Path]::GetTempPath()) 'unused-controlled-peer-evidence.json') `
        -ValidateOnly 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Controlled-peer validation-only invocation failed: $($output -join [Environment]::NewLine)"
    }
    $joinedOutput = $output -join [Environment]::NewLine
    foreach ($value in $fakeValues.Values) {
        if ($joinedOutput.IndexOf([string]$value, [StringComparison]::Ordinal) -ge 0) {
            throw 'Controlled-peer validation-only invocation exposed a bundle value.'
        }
    }
}
finally {
    [Environment]::SetEnvironmentVariable('SOCKSEEK_LIVE_PEER_BUNDLE', $previousBundle)
}

Write-Host 'Soulseek controlled remote-peer workflow smoke passed.'
