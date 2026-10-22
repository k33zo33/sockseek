param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$generator = Join-Path $PSScriptRoot 'new_soulseek_live_checklist.ps1'
$validator = Join-Path $PSScriptRoot 'validate_soulseek_live_checklist.ps1'
$powerShellExecutable = if ($PSVersionTable.PSEdition -eq 'Core') {
    Join-Path $PSHOME ('pwsh' + $(if ($IsWindows) { '.exe' } else { '' }))
}
else {
    'powershell'
}
foreach ($script in @($generator, $validator)) {
    if (-not (Test-Path -LiteralPath $script -PathType Leaf)) {
        throw "Required script was not found: $script"
    }
}

function Invoke-PowerShellScript {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string[]]$ScriptArguments
    )

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & $powerShellExecutable -NoProfile -ExecutionPolicy Bypass -File $Path @ScriptArguments *> $null
        return $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
}

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('sockseek-live-checklist-' + [Guid]::NewGuid().ToString('N'))
$stage = Join-Path $tempRoot 'stage'
$outputRoot = Join-Path $tempRoot 'runs'
$artifact = Join-Path $tempRoot 'Sockseek-win-x64.zip'
$commit = '0123456789abcdef0123456789abcdef01234567'
New-Item -ItemType Directory -Path $stage | Out-Null

try {
    $releaseMetadata = [ordered]@{
        version = '3.0.5-test'
        commit = $commit
        sourceUrl = 'https://github.com/k33zo33/sockseek'
        license = 'AGPL-3.0'
    }
    $releaseMetadata | ConvertTo-Json -Compress | Set-Content -LiteralPath (Join-Path $stage 'release-metadata.json') -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $stage 'Sockseek.Desktop.exe') -Value 'fixture' -Encoding ASCII
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $artifact

    $generatorArguments = @(
        '-ArtifactPath', $artifact,
        '-Reviewer', 'Release QA',
        '-SessionLabel', 'dedicated-live-01',
        '-ExpectedCommit', $commit,
        '-OutputRoot', $outputRoot
    )
    if ((Invoke-PowerShellScript -Path $generator -ScriptArguments $generatorArguments) -ne 0) {
        throw 'Expected live checklist generator to accept a valid package.'
    }

    $runDirectories = @(Get-ChildItem -LiteralPath $outputRoot -Directory)
    if ($runDirectories.Count -ne 1) {
        throw "Expected one generated run directory, found $($runDirectories.Count)."
    }
    $runDirectory = $runDirectories[0].FullName
    $checklistPath = Join-Path $runDirectory 'checklist.md'
    $metadataPath = Join-Path $runDirectory 'run-metadata.json'
    if (-not (Test-Path -LiteralPath $checklistPath) -or -not (Test-Path -LiteralPath $metadataPath)) {
        throw 'Generator did not create the checklist and run metadata files.'
    }

    $validatorArguments = @('-ArtifactPath', $artifact, '-RunDirectory', $runDirectory)
    if ((Invoke-PowerShellScript -Path $validator -ScriptArguments $validatorArguments) -eq 0) {
        throw 'Expected validator to reject a checklist with pending rows.'
    }

    $checklist = Get-Content -LiteralPath $checklistPath -Raw
    $checklist = [regex]::Replace(
        $checklist,
        '(?m)^(\|\s*(?:Search|Wishlist|Download|Upload / sharing|Chat|Privileges|Folder browse|Sharing counts/status|Legal-use messaging|Provider audio invariant)\s*\|)\s*Pending\s*\|\s*TODO\s*\|\s*TODO\s*\|$',
        '$1 Pass | 2026-10-21T12:00:00Z | redacted manual smoke evidence |')
    $checklist = $checklist `
        -replace '(?m)^- Start time UTC: TODO$', '- Start time UTC: 2026-10-21T11:30:00Z' `
        -replace '(?m)^- End time UTC: TODO$', '- End time UTC: 2026-10-21T12:30:00Z' `
        -replace '(?m)^- Diagnostics export reviewed and redacted: TODO$', '- Diagnostics export reviewed and redacted: Pass; evidence/diagnostics-review.txt' `
        -replace '(?m)^- Automated gate transcript or exact reference: TODO$', '- Automated gate transcript or exact reference: evidence/automated-gates.txt' `
        -replace '(?m)^- Failures, retries or deviations: TODO$', '- Failures, retries or deviations: None observed'
    Set-Content -LiteralPath $checklistPath -Value $checklist -Encoding UTF8

    if ((Invoke-PowerShellScript -Path $validator -ScriptArguments $validatorArguments) -ne 0) {
        throw 'Expected validator to accept complete redacted evidence.'
    }

    $originalMetadataJson = Get-Content -LiteralPath $metadataPath -Raw
    $forgedMetadata = $originalMetadataJson | ConvertFrom-Json
    $forgedMetadata.requiredHost = 'Windows11X64'
    $forgedMetadata.host.family = 'Windows'
    $forgedMetadata.host.description = 'Microsoft Windows 11 Pro'
    $forgedMetadata.host.version = '10.0.22631 (build 22631)'
    $forgedMetadata.host.architecture = 'X64'
    $forgedMetadata.host.classification = 'Windows11X64'
    $forgedMetadata | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $metadataPath -Encoding UTF8
    $windowsChecklist = [regex]::Replace(
        $checklist,
        '(?m)^\| Host \|.*$',
        '| Host | Microsoft Windows 11 Pro; 10.0.22631 (build 22631); X64; Windows11X64 |')
    Set-Content -LiteralPath $checklistPath -Value $windowsChecklist -Encoding UTF8
    if ((Invoke-PowerShellScript -Path $validator -ScriptArguments $validatorArguments) -ne 0) {
        throw 'Expected validator to accept coherent Windows 11 x64 host evidence.'
    }

    Set-Content -LiteralPath $checklistPath -Value $checklist -Encoding UTF8
    if ((Invoke-PowerShellScript -Path $validator -ScriptArguments $validatorArguments) -eq 0) {
        throw 'Expected validator to reject host metadata that no longer matches the checklist header.'
    }

    $forgedMetadata.host.version = '10.0.19045 (build 19045)'
    $forgedMetadata | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $metadataPath -Encoding UTF8
    if ((Invoke-PowerShellScript -Path $validator -ScriptArguments $validatorArguments) -eq 0) {
        throw 'Expected validator to reject a Windows 11 label with a pre-Windows 11 build.'
    }

    $forgedMetadata.host.family = 'Linux'
    $forgedMetadata.host.description = 'Ubuntu 24.04 LTS'
    $forgedMetadata.host.version = '24.04'
    $forgedMetadata.host.architecture = 'X64'
    $forgedMetadata.host.classification = 'Ubuntu2404X64'
    $forgedMetadata.requiredHost = 'Ubuntu2404X64'
    $forgedMetadata | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $metadataPath -Encoding UTF8
    $ubuntuChecklist = [regex]::Replace(
        $checklist,
        '(?m)^\| Host \|.*$',
        '| Host | Ubuntu 24.04 LTS; 24.04; X64; Ubuntu2404X64 |')
    Set-Content -LiteralPath $checklistPath -Value $ubuntuChecklist -Encoding UTF8
    if ((Invoke-PowerShellScript -Path $validator -ScriptArguments $validatorArguments) -ne 0) {
        throw 'Expected validator to accept coherent Ubuntu 24.04 x64 host evidence.'
    }

    $forgedMetadata.host.version = '22.04'
    $forgedMetadata | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $metadataPath -Encoding UTF8
    if ((Invoke-PowerShellScript -Path $validator -ScriptArguments $validatorArguments) -eq 0) {
        throw 'Expected validator to reject Ubuntu 22.04 metadata labeled as Ubuntu 24.04.'
    }

    Set-Content -LiteralPath $metadataPath -Value $originalMetadataJson -Encoding UTF8
    Set-Content -LiteralPath $checklistPath -Value $checklist -Encoding UTF8

    Set-Content -LiteralPath (Join-Path $runDirectory 'evidence\diagnostics-review.txt') `
        -Value 'password=should-not-appear' `
        -Encoding ASCII
    if ((Invoke-PowerShellScript -Path $validator -ScriptArguments $validatorArguments) -eq 0) {
        throw 'Expected validator to reject a password assignment in text evidence.'
    }

    Set-Content -LiteralPath (Join-Path $runDirectory 'evidence\diagnostics-review.txt') `
        -Value '{"password":"should-not-appear"}' `
        -Encoding ASCII
    if ((Invoke-PowerShellScript -Path $validator -ScriptArguments $validatorArguments) -eq 0) {
        throw 'Expected validator to reject a quoted JSON password property.'
    }

    Set-Content -LiteralPath (Join-Path $runDirectory 'evidence\diagnostics-review.txt') `
        -Value '{"clientSecret":"should-not-appear"}' `
        -Encoding ASCII
    if ((Invoke-PowerShellScript -Path $validator -ScriptArguments $validatorArguments) -eq 0) {
        throw 'Expected validator to reject a camelCase JSON secret property.'
    }

    Set-Content -LiteralPath (Join-Path $runDirectory 'evidence\diagnostics-review.txt') `
        -Value '{"code":"should-not-appear"}' `
        -Encoding ASCII
    if ((Invoke-PowerShellScript -Path $validator -ScriptArguments $validatorArguments) -eq 0) {
        throw 'Expected validator to reject a quoted OAuth code property.'
    }

    Set-Content -LiteralPath (Join-Path $runDirectory 'evidence\diagnostics-review.txt') `
        -Value '--password should-not-appear' `
        -Encoding ASCII
    if ((Invoke-PowerShellScript -Path $validator -ScriptArguments $validatorArguments) -eq 0) {
        throw 'Expected validator to reject a password command-line option.'
    }
    Remove-Item -LiteralPath (Join-Path $runDirectory 'evidence\diagnostics-review.txt') -Force

    Add-Content -LiteralPath $checklistPath -Value "`nAuthorization: Bearer should-not-appear"
    if ((Invoke-PowerShellScript -Path $validator -ScriptArguments $validatorArguments) -eq 0) {
        throw 'Expected validator to reject an Authorization bearer header.'
    }
    Set-Content -LiteralPath $checklistPath -Value $checklist -Encoding UTF8

    Add-Content -LiteralPath $checklistPath -Value "`n{`"Authorization`":`"Basic should-not-appear`"}"
    if ((Invoke-PowerShellScript -Path $validator -ScriptArguments $validatorArguments) -eq 0) {
        throw 'Expected validator to reject a quoted Basic Authorization credential.'
    }
    Set-Content -LiteralPath $checklistPath -Value $checklist -Encoding UTF8

    $stream = [IO.File]::Open($artifact, [IO.FileMode]::Append, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $stream.WriteByte(0)
    }
    finally {
        $stream.Dispose()
    }
    if ((Invoke-PowerShellScript -Path $validator -ScriptArguments $validatorArguments) -eq 0) {
        throw 'Expected validator to reject an artifact changed after evidence creation.'
    }

    $mismatchOutput = Join-Path $tempRoot 'mismatch-runs'
    $mismatchArguments = @(
        '-ArtifactPath', $artifact,
        '-Reviewer', 'Release QA',
        '-SessionLabel', 'dedicated-live-02',
        '-ExpectedCommit', 'ffffffffffffffffffffffffffffffffffffffff',
        '-OutputRoot', $mismatchOutput
    )
    if ((Invoke-PowerShellScript -Path $generator -ScriptArguments $mismatchArguments) -eq 0) {
        throw 'Expected generator to reject an unexpected artifact commit.'
    }

    $global:LASTEXITCODE = 0
    Write-Host 'Soulseek live checklist helper smoke passed.'
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}

