param(
    [string]$Root = '.'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$rootPath = (Resolve-Path -LiteralPath $Root).Path
$orchestratorPath = Join-Path $rootPath 'scripts/run_soulseek_live_checklist.ps1'
if (-not (Test-Path -LiteralPath $orchestratorPath -PathType Leaf)) {
    throw "Controlled live-checklist orchestrator is missing: $orchestratorPath"
}

$orchestrator = Get-Content -LiteralPath $orchestratorPath -Raw

function Assert-Contains {
    param([string]$Text, [string]$Value, [string]$Description)
    if ($Text.IndexOf($Value, [StringComparison]::Ordinal) -lt 0) {
        throw "Controlled live-checklist orchestrator is missing $Description."
    }
}

Assert-Contains $orchestrator '[switch]$PortForwardConfirmed' 'explicit port-forward confirmation'
Assert-Contains $orchestrator 'ExpectedArtifactSha256' 'an approved full artifact-hash input'
Assert-Contains $orchestrator 'The retained artifact SHA256 does not match' 'fail-closed artifact verification'
Assert-Contains $orchestrator "Join-Path `$packageRoot 'daemon\Sockseek.Server.exe'" 'execution from the exact extracted package'
Assert-Contains $orchestrator "'SockseekServer__Engine__ListenPort'" 'the approved temporary listener binding'
Assert-Contains $orchestrator '$bundleJson | & gh secret set $secretName' 'standard-input-only repository secret creation'
Assert-Contains $orchestrator "'secret', 'delete', `$secretName" 'repository secret deletion'
Assert-Contains $orchestrator "'run', 'cancel'" 'failed remote-workflow cancellation'
Assert-Contains $orchestrator 'Remove-Item -LiteralPath $workRoot -Recurse -Force' 'bounded sensitive working-data cleanup'
Assert-Contains $orchestrator 'escaped the temporary directory' 'an explicit temporary-directory cleanup boundary check'
Assert-Contains $orchestrator "-Path '/api/v1/soulseek/account'" 'explicit host account cleanup'
Assert-Contains $orchestrator 'Complete-Checklist' 'artifact-bound checklist finalization'
Assert-Contains $orchestrator 'validate_soulseek_live_checklist.ps1' 'final checklist validation'
Assert-Contains $orchestrator 'remoteEvidence.cleanup.accountDisconnected' 'remote cleanup evidence verification'
Assert-Contains $orchestrator 'repositoryControlRemoved' 'verified temporary-control cleanup evidence'

if ($orchestrator -match '(?im)gh\s+secret\s+set[^\r\n]*(?:--body|-b)') {
    throw 'The controlled peer bundle must never be passed on a process command line.'
}
if ($orchestrator -match '(?im)Write-(Host|Output|Verbose|Debug|Information|Warning)\s+.*(?:bundleJson|remoteUsername|remotePassword|hostUsername|hostPassword|readyMessage|replyMessage|completeMessage)') {
    throw 'The orchestrator may not print generated control values.'
}
if ($orchestrator -match '(?im)ConvertTo-Json.*(?:result).*?(?:remoteUsername|remotePassword|hostUsername|hostPassword|readyMessage|replyMessage|completeMessage)') {
    throw 'The orchestrator may not serialize generated control values into evidence.'
}
$remoteChecks = [regex]::Match($orchestrator, '(?s)\$remoteChecks\s*=\s*@\((?<body>.*?)\n\s*\)')
if (-not $remoteChecks.Success -or $remoteChecks.Groups['body'].Value -match '(?m),\s*$') {
    throw 'Remote evidence checks must remain independent Boolean pipeline outputs, not one comma-bound expression.'
}

$hostExecutable = (Get-Process -Id $PID).Path
$output = & $hostExecutable `
    -NoProfile `
    -File $orchestratorPath `
    -ArtifactPath 'validation-only' `
    -Reviewer 'release-reviewer' `
    -CandidateCommit 'c7b4cc5ba18cc3ce4f9c2b3fa83734b07d67ac0f' `
    -ExpectedArtifactSha256 '2d41a5f4cedd496ff8ea3e7adac13347634eb922aa7e6f1c07289ab64c210dde' `
    -RunLabel 'validation-run' `
    -PortForwardConfirmed `
    -ValidateOnly 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Controlled live-checklist validation-only invocation failed: $($output -join [Environment]::NewLine)"
}

Write-Host 'Soulseek controlled live-checklist orchestrator smoke passed.'
