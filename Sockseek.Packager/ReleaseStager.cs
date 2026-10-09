using System.Text.Json;

namespace Sockseek.Packager;

public static class ReleaseStager
{
    public static ReleaseStageResult StageWindows(ReleaseStageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<string>();
        var repoRoot = Path.GetFullPath(request.RepositoryRoot);
        var desktopPublishDirectory = Path.GetFullPath(request.DesktopPublishDirectory);
        var daemonPublishDirectory = Path.GetFullPath(request.DaemonPublishDirectory);
        var stagingDirectory = Path.GetFullPath(request.StagingDirectory);
        var sbomPath = Path.GetFullPath(request.SbomPath);

        RequireDirectory(repoRoot, "Repository root", errors);
        RequireDirectory(desktopPublishDirectory, "Desktop publish directory", errors);
        RequireDirectory(daemonPublishDirectory, "Daemon publish directory", errors);
        RequireFile(Path.Combine(repoRoot, ReleaseArtifactValidator.LicenseFileName), "LICENSE", errors);
        RequireFile(Path.Combine(repoRoot, ReleaseArtifactValidator.ThirdPartyNoticesFileName), "THIRD-PARTY-NOTICES", errors);
        RequireFile(Path.Combine(repoRoot, ReleaseArtifactValidator.SecurityFileName), "SECURITY.md", errors);
        RequireFile(Path.Combine(repoRoot, ReleaseArtifactValidator.BetaLimitationsFileName), "docs/beta-limitations.md", errors);
        RequireFile(Path.Combine(desktopPublishDirectory, request.DesktopExecutableName), "Desktop executable", errors);
        RequireFile(Path.Combine(daemonPublishDirectory, request.DaemonExecutableName), "Daemon executable", errors);
        RequireFile(sbomPath, "SBOM", errors);

        if (IsSameOrUnderDirectory(desktopPublishDirectory, stagingDirectory))
            errors.Add("Staging directory must not be inside the Desktop publish directory.");
        if (IsSameOrUnderDirectory(daemonPublishDirectory, stagingDirectory))
            errors.Add("Staging directory must not be inside the daemon publish directory.");

        if (Directory.Exists(stagingDirectory) && Directory.EnumerateFileSystemEntries(stagingDirectory).Any())
            errors.Add($"Staging directory must be empty: {stagingDirectory}");

        if (errors.Count > 0)
            return new ReleaseStageResult(stagingDirectory, errors);

        Directory.CreateDirectory(stagingDirectory);
        CopyDirectory(desktopPublishDirectory, stagingDirectory);

        string daemonStagingDirectory = Path.Combine(stagingDirectory, "daemon");
        Directory.CreateDirectory(daemonStagingDirectory);
        CopyDirectory(daemonPublishDirectory, daemonStagingDirectory);

        CopyFile(repoRoot, stagingDirectory, ReleaseArtifactValidator.LicenseFileName);
        CopyFile(repoRoot, stagingDirectory, ReleaseArtifactValidator.ThirdPartyNoticesFileName);
        CopyFile(repoRoot, stagingDirectory, ReleaseArtifactValidator.SecurityFileName);
        CopyFile(repoRoot, stagingDirectory, ReleaseArtifactValidator.BetaLimitationsFileName);
        File.Copy(sbomPath, Path.Combine(stagingDirectory, ReleaseArtifactValidator.SbomFileName), overwrite: true);
        File.WriteAllText(Path.Combine(stagingDirectory, ReleaseArtifactValidator.WindowsInstallerFileName), WindowsInstallScript);
        File.WriteAllText(Path.Combine(stagingDirectory, ReleaseArtifactValidator.WindowsUninstallerFileName), WindowsUninstallScript);

        var metadata = new ReleaseMetadata(
            request.Version,
            request.Commit,
            request.SourceUrl,
            "AGPL-3.0");
        File.WriteAllText(
            Path.Combine(stagingDirectory, ReleaseArtifactValidator.MetadataFileName),
            JsonSerializer.Serialize(metadata, ReleaseMetadataJsonContext.Default.ReleaseMetadata));

        var validation = ReleaseArtifactValidator.Validate(
            stagingDirectory,
            request.DesktopExecutableName,
            Path.Combine("daemon", request.DaemonExecutableName));

        return validation.IsValid
            ? new ReleaseStageResult(stagingDirectory, [])
            : new ReleaseStageResult(stagingDirectory, validation.Errors);
    }

    private static void RequireDirectory(string path, string label, List<string> errors)
    {
        if (!Directory.Exists(path))
            errors.Add($"{label} does not exist: {path}");
    }

    private static void RequireFile(string path, string label, List<string> errors)
    {
        if (!File.Exists(path))
            errors.Add($"{label} does not exist: {path}");
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, file);
            string destinationPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath, overwrite: true);
        }
    }

    private static void CopyFile(string sourceRoot, string destinationRoot, string relativePath)
    {
        string destinationPath = Path.Combine(destinationRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Copy(Path.Combine(sourceRoot, relativePath), destinationPath, overwrite: true);
    }

    private static bool IsSameOrUnderDirectory(string directory, string path)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var fullDirectory = Path.GetFullPath(directory);
        var fullPath = Path.GetFullPath(path);
        if (string.Equals(fullDirectory, fullPath, comparison))
            return true;

        if (!fullDirectory.EndsWith(Path.DirectorySeparatorChar))
            fullDirectory += Path.DirectorySeparatorChar;

        return fullPath.StartsWith(fullDirectory, comparison);
    }

    private const string WindowsInstallScript =
        """
        #Requires -Version 5.1
        [CmdletBinding()]
        param(
            [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\Sockseek'),
            [string]$DataDir = (Join-Path $env:LOCALAPPDATA 'Sockseek'),
            [string]$ShortcutDir = (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs')
        )

        $ErrorActionPreference = 'Stop'
        $source = Split-Path -Parent $MyInvocation.MyCommand.Path
        $installDirFull = [System.IO.Path]::GetFullPath($InstallDir)
        $dataDirFull = [System.IO.Path]::GetFullPath($DataDir)

        New-Item -ItemType Directory -Force -Path $installDirFull | Out-Null
        Copy-Item -Path (Join-Path $source '*') -Destination $installDirFull -Recurse -Force

        foreach ($child in @('config', 'logs', 'backups', 'secrets')) {
            New-Item -ItemType Directory -Force -Path (Join-Path $dataDirFull $child) | Out-Null
        }

        $shortcutDirFull = [System.IO.Path]::GetFullPath($ShortcutDir)
        New-Item -ItemType Directory -Force -Path $shortcutDirFull | Out-Null
        $shortcutPath = Join-Path $shortcutDirFull 'Sockseek.lnk'
        $shell = New-Object -ComObject WScript.Shell
        $shortcut = $shell.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = Join-Path $installDirFull 'Sockseek.Desktop.exe'
        $shortcut.WorkingDirectory = $installDirFull
        $shortcut.Description = 'Sockseek local-first desktop music manager'
        $shortcut.Save()

        Write-Host "Sockseek installed to $installDirFull"
        Write-Host "User data directory: $dataDirFull"
        """;

    private const string WindowsUninstallScript =
        """
        #Requires -Version 5.1
        [CmdletBinding()]
        param(
            [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\Sockseek'),
            [string]$DataDir = (Join-Path $env:LOCALAPPDATA 'Sockseek'),
            [string]$ShortcutDir = (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'),
            [switch]$RemoveUserData
        )

        $ErrorActionPreference = 'Stop'
        $installDirFull = [System.IO.Path]::GetFullPath($InstallDir)
        $dataDirFull = [System.IO.Path]::GetFullPath($DataDir)
        $shortcutPath = Join-Path ([System.IO.Path]::GetFullPath($ShortcutDir)) 'Sockseek.lnk'

        Remove-Item -LiteralPath $shortcutPath -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $installDirFull -Recurse -Force -ErrorAction SilentlyContinue

        if ($RemoveUserData) {
            Remove-Item -LiteralPath $dataDirFull -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "Sockseek user data removed from $dataDirFull"
        }
        else {
            Write-Host "Sockseek user data preserved at $dataDirFull"
        }
        """;
}

public sealed record ReleaseStageRequest(
    string RepositoryRoot,
    string DesktopPublishDirectory,
    string DaemonPublishDirectory,
    string StagingDirectory,
    string DesktopExecutableName,
    string DaemonExecutableName,
    string Version,
    string Commit,
    string SourceUrl,
    string SbomPath);

public sealed record ReleaseStageResult(
    string StagingDirectory,
    IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
