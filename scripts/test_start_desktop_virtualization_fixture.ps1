param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$launcher = Join-Path $PSScriptRoot 'start_desktop_virtualization_fixture.ps1'

if (-not (Test-Path -LiteralPath $launcher)) {
    throw "Launcher script was not found: $launcher"
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("sockseek-desktop-trace-launcher-" + [System.Guid]::NewGuid().ToString('N'))
$configDir = Join-Path $tempRoot 'config'
$mockSoulseekDir = Join-Path $tempRoot 'mock-soulseek'
New-Item -ItemType Directory -Path $configDir | Out-Null
New-Item -ItemType Directory -Path $mockSoulseekDir | Out-Null
Set-Content -LiteralPath (Join-Path $configDir 'sockseek.db') -Value 'fixture placeholder' -NoNewline -Encoding ASCII

function Invoke-Launcher {
    param([string[]]$ExtraArguments)

    $stdout = Join-Path $tempRoot ("launcher-out-" + [System.Guid]::NewGuid().ToString('N') + ".txt")
    $stderr = Join-Path $tempRoot ("launcher-err-" + [System.Guid]::NewGuid().ToString('N') + ".txt")
    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        "`"$launcher`"",
        '-ConfigDir',
        "`"$configDir`"",
        '-MockSoulseekDir',
        "`"$mockSoulseekDir`"",
        '-PrintOnly'
    ) + $ExtraArguments

    $process = Start-Process -FilePath 'powershell' `
        -ArgumentList ($arguments -join ' ') `
        -Wait `
        -PassThru `
        -NoNewWindow `
        -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr

    [pscustomobject]@{
        ExitCode = $process.ExitCode
        Output = Get-Content -LiteralPath $stdout -Raw
        Error = Get-Content -LiteralPath $stderr -Raw
    }
}

try {
    $developerResult = Invoke-Launcher -ExtraArguments @()
    if ($developerResult.ExitCode -ne 0) {
        throw "Expected developer launcher print-only run to pass. Error: $($developerResult.Error)"
    }

    if ($developerResult.Output.IndexOf('Launch mode: developer rehearsal', [System.StringComparison]::Ordinal) -lt 0) {
        throw 'Expected developer launcher output to identify developer rehearsal mode.'
    }

    if ($developerResult.Output.IndexOf('SOCKSEEK_DESKTOP_CONFIG_DIR=', [System.StringComparison]::Ordinal) -lt 0 -or
        $developerResult.Output.IndexOf('SockseekServer__Engine__MockFilesDir=', [System.StringComparison]::Ordinal) -lt 0 -or
        $developerResult.Output.IndexOf('SockseekServer__Engine__MockFilesReadTags=false', [System.StringComparison]::Ordinal) -lt 0) {
        throw 'Expected developer launcher output to include fixture environment variables.'
    }

    $desktopExecutablePath = (Get-Command powershell.exe).Source
    $packagedResult = Invoke-Launcher -ExtraArguments @('-DesktopExecutablePath', "`"$desktopExecutablePath`"")
    if ($packagedResult.ExitCode -ne 0) {
        throw "Expected packaged launcher print-only run to pass. Error: $($packagedResult.Error)"
    }

    if ($packagedResult.Output.IndexOf('Launch mode: packaged executable', [System.StringComparison]::Ordinal) -lt 0) {
        throw 'Expected packaged launcher output to identify packaged executable mode.'
    }

    Write-Host 'Desktop virtualization fixture launcher smoke passed.'
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
