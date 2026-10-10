using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Packager;
using System.IO.Compression;

namespace Tests.Packager;

[TestClass]
public sealed class ReleaseStagerTests
{
    [TestMethod]
    public void StageWindows_WithPublishedDesktopDaemonAndReleaseArtifacts_CreatesValidStagingDirectory()
    {
        using var temp = TempDirectory.Create();
        string repoRoot = Path.Combine(temp.Path, "repo");
        string desktopPublish = Path.Combine(temp.Path, "desktop");
        string daemonPublish = Path.Combine(temp.Path, "daemon");
        string staging = Path.Combine(temp.Path, "staging");
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(desktopPublish);
        Directory.CreateDirectory(daemonPublish);
        Directory.CreateDirectory(Path.Combine(daemonPublish, "native"));

        WriteRequiredRepoArtifacts(repoRoot);
        File.WriteAllText(Path.Combine(desktopPublish, "Sockseek.Desktop.exe"), "desktop");
        File.WriteAllText(Path.Combine(desktopPublish, "Avalonia.dll"), "desktop dependency");
        File.WriteAllText(Path.Combine(daemonPublish, "Sockseek.Server.exe"), "daemon");
        File.WriteAllText(Path.Combine(daemonPublish, "native", "e_sqlite3.dll"), "sqlite native");
        string sbomPath = Path.Combine(temp.Path, "sbom.spdx.json");
        WriteValidSbom(sbomPath);

        var result = ReleaseStager.StageWindows(new ReleaseStageRequest(
            repoRoot,
            desktopPublish,
            daemonPublish,
            staging,
            "Sockseek.Desktop.exe",
            "Sockseek.Server.exe",
            "3.0.5",
            "abc123",
            "https://github.com/k33zo33/sockseek",
            sbomPath));

        Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        Assert.IsTrue(File.Exists(Path.Combine(staging, "Sockseek.Desktop.exe")));
        Assert.IsTrue(File.Exists(Path.Combine(staging, "Avalonia.dll")));
        Assert.IsTrue(File.Exists(Path.Combine(staging, "daemon", "Sockseek.Server.exe")));
        Assert.IsTrue(File.Exists(Path.Combine(staging, "daemon", "native", "e_sqlite3.dll")));
        Assert.IsTrue(File.Exists(Path.Combine(staging, ReleaseArtifactValidator.LicenseFileName)));
        Assert.IsTrue(File.Exists(Path.Combine(staging, ReleaseArtifactValidator.ThirdPartyNoticesFileName)));
        Assert.IsTrue(File.Exists(Path.Combine(staging, ReleaseArtifactValidator.SecurityFileName)));
        Assert.IsTrue(File.Exists(Path.Combine(staging, ReleaseArtifactValidator.BetaLimitationsFileName)));
        Assert.IsTrue(File.Exists(Path.Combine(staging, ReleaseArtifactValidator.SbomFileName)));
        Assert.IsTrue(File.Exists(Path.Combine(staging, ReleaseArtifactValidator.MetadataFileName)));
        Assert.IsTrue(File.Exists(Path.Combine(staging, ReleaseArtifactValidator.WindowsInstallerFileName)));
        Assert.IsTrue(File.Exists(Path.Combine(staging, ReleaseArtifactValidator.WindowsUninstallerFileName)));
        StringAssert.Contains(
            File.ReadAllText(Path.Combine(staging, ReleaseArtifactValidator.WindowsInstallerFileName)),
            "Programs\\Sockseek");
        StringAssert.Contains(
            File.ReadAllText(Path.Combine(staging, ReleaseArtifactValidator.WindowsUninstallerFileName)),
            "Sockseek user data preserved");
    }

    [TestMethod]
    public void StageWindows_WhenRequiredInputsAreMissing_ReturnsActionableErrors()
    {
        using var temp = TempDirectory.Create();

        var result = ReleaseStager.StageWindows(new ReleaseStageRequest(
            Path.Combine(temp.Path, "repo"),
            Path.Combine(temp.Path, "desktop"),
            Path.Combine(temp.Path, "daemon"),
            Path.Combine(temp.Path, "staging"),
            "Sockseek.Desktop.exe",
            "Sockseek.Server.exe",
            "3.0.5",
            "abc123",
            "https://github.com/k33zo33/sockseek",
            Path.Combine(temp.Path, "sbom.spdx.json")));

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(error => error.StartsWith("Repository root does not exist:", StringComparison.Ordinal)));
        Assert.IsTrue(result.Errors.Any(error => error.StartsWith("Desktop publish directory does not exist:", StringComparison.Ordinal)));
        Assert.IsTrue(result.Errors.Any(error => error.StartsWith("Daemon publish directory does not exist:", StringComparison.Ordinal)));
        Assert.IsTrue(result.Errors.Any(error => error.StartsWith("SBOM does not exist:", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void StageWindows_WhenStagingDirectoryIsInsidePublishDirectory_ReturnsError()
    {
        using var temp = TempDirectory.Create();
        string repoRoot = Path.Combine(temp.Path, "repo");
        string desktopPublish = Path.Combine(temp.Path, "desktop");
        string daemonPublish = Path.Combine(temp.Path, "daemon");
        string staging = Path.Combine(desktopPublish, "staging");
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(desktopPublish);
        Directory.CreateDirectory(daemonPublish);
        WriteRequiredRepoArtifacts(repoRoot);
        File.WriteAllText(Path.Combine(desktopPublish, "Sockseek.Desktop.exe"), "desktop");
        File.WriteAllText(Path.Combine(daemonPublish, "Sockseek.Server.exe"), "daemon");
        string sbomPath = Path.Combine(temp.Path, "sbom.spdx.json");
        WriteValidSbom(sbomPath);

        var result = ReleaseStager.StageWindows(new ReleaseStageRequest(
            repoRoot,
            desktopPublish,
            daemonPublish,
            staging,
            "Sockseek.Desktop.exe",
            "Sockseek.Server.exe",
            "3.0.5",
            "abc123",
            "https://github.com/k33zo33/sockseek",
            sbomPath));

        Assert.IsFalse(result.IsValid);
        CollectionAssert.Contains(result.Errors.ToArray(), "Staging directory must not be inside the Desktop publish directory.");
    }

    [TestMethod]
    public void WindowsInstallerScripts_InstallAndUninstallTempPayload()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var temp = TempDirectory.Create();
        string repoRoot = Path.Combine(temp.Path, "repo");
        string desktopPublish = Path.Combine(temp.Path, "desktop");
        string daemonPublish = Path.Combine(temp.Path, "daemon");
        string staging = Path.Combine(temp.Path, "staging");
        string installDir = Path.Combine(temp.Path, "install");
        string dataDir = Path.Combine(temp.Path, "data");
        string shortcutDir = Path.Combine(temp.Path, "shortcuts");
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(desktopPublish);
        Directory.CreateDirectory(daemonPublish);
        WriteRequiredRepoArtifacts(repoRoot);
        File.WriteAllText(Path.Combine(desktopPublish, "Sockseek.Desktop.exe"), "desktop");
        File.WriteAllText(Path.Combine(daemonPublish, "Sockseek.Server.exe"), "daemon");
        string sbomPath = Path.Combine(temp.Path, "sbom.spdx.json");
        WriteValidSbom(sbomPath);

        var result = ReleaseStager.StageWindows(new ReleaseStageRequest(
            repoRoot,
            desktopPublish,
            daemonPublish,
            staging,
            "Sockseek.Desktop.exe",
            "Sockseek.Server.exe",
            "3.0.5",
            "abc123",
            "https://github.com/k33zo33/sockseek",
            sbomPath));
        Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.Errors));

        RunPowerShellScript(
            Path.Combine(staging, ReleaseArtifactValidator.WindowsInstallerFileName),
            "-InstallDir", installDir,
            "-DataDir", dataDir,
            "-ShortcutDir", shortcutDir);

        Assert.IsTrue(File.Exists(Path.Combine(installDir, "Sockseek.Desktop.exe")));
        Assert.IsTrue(Directory.Exists(Path.Combine(dataDir, "config")));
        Assert.IsTrue(Directory.Exists(Path.Combine(dataDir, "logs")));
        Assert.IsTrue(Directory.Exists(Path.Combine(dataDir, "backups")));
        Assert.IsTrue(Directory.Exists(Path.Combine(dataDir, "secrets")));
        Assert.IsTrue(File.Exists(Path.Combine(shortcutDir, "Sockseek.lnk")));

        RunPowerShellScript(
            Path.Combine(staging, ReleaseArtifactValidator.WindowsUninstallerFileName),
            "-InstallDir", installDir,
            "-DataDir", dataDir,
            "-ShortcutDir", shortcutDir);

        Assert.IsFalse(Directory.Exists(installDir));
        Assert.IsTrue(Directory.Exists(dataDir));
        Assert.IsFalse(File.Exists(Path.Combine(shortcutDir, "Sockseek.lnk")));

        RunPowerShellScript(
            Path.Combine(staging, ReleaseArtifactValidator.WindowsUninstallerFileName),
            "-InstallDir", installDir,
            "-DataDir", dataDir,
            "-ShortcutDir", shortcutDir,
            "-RemoveUserData");

        Assert.IsFalse(Directory.Exists(dataDir));
    }

    [TestMethod]
    public void ArchiveWindows_WithValidStaging_CreatesZipAndSha256Manifest()
    {
        using var temp = TempDirectory.Create();
        string staging = CreateValidWindowsStaging(temp);
        string archivePath = Path.Combine(temp.Path, "Sockseek-win-x64.zip");
        string manifestPath = Path.Combine(temp.Path, "Sockseek-win-x64.sha256");

        var result = ReleaseArchiveBuilder.ArchiveWindows(new ReleaseArchiveRequest(
            staging,
            archivePath,
            manifestPath,
            "Sockseek.Desktop.exe",
            "Sockseek.Server.exe"));

        Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        Assert.IsTrue(File.Exists(archivePath));
        Assert.IsTrue(File.Exists(manifestPath));
        using var archive = ZipFile.OpenRead(archivePath);
        CollectionAssert.Contains(
            archive.Entries.Select(entry => entry.FullName).ToArray(),
            "Sockseek.Desktop.exe");
        CollectionAssert.Contains(
            archive.Entries.Select(entry => entry.FullName).ToArray(),
            "daemon/Sockseek.Server.exe");
        CollectionAssert.Contains(
            archive.Entries.Select(entry => entry.FullName).ToArray(),
            "install.ps1");
        CollectionAssert.Contains(
            archive.Entries.Select(entry => entry.FullName).ToArray(),
            ReleaseArtifactValidator.SecurityFileName);
        CollectionAssert.Contains(
            archive.Entries.Select(entry => entry.FullName).ToArray(),
            ReleaseArtifactValidator.BetaLimitationsFileName.Replace(Path.DirectorySeparatorChar, '/'));
        var manifest = File.ReadAllText(manifestPath);
        StringAssert.Contains(manifest, "Sockseek-win-x64.zip");
        StringAssert.Contains(manifest, "Sockseek.Desktop.exe");
        StringAssert.Contains(manifest, "daemon/Sockseek.Server.exe");
        StringAssert.Contains(manifest, "sbom.spdx.json");
        StringAssert.Contains(manifest, ReleaseArtifactValidator.SecurityFileName);
        StringAssert.Contains(manifest, ReleaseArtifactValidator.BetaLimitationsFileName.Replace(Path.DirectorySeparatorChar, '/'));
    }

    [TestMethod]
    public void ArchiveWindows_WhenArchiveWouldBeInsideStaging_ReturnsError()
    {
        using var temp = TempDirectory.Create();
        string staging = CreateValidWindowsStaging(temp);
        string archivePath = Path.Combine(staging, "Sockseek-win-x64.zip");
        string manifestPath = Path.Combine(temp.Path, "Sockseek-win-x64.sha256");

        var result = ReleaseArchiveBuilder.ArchiveWindows(new ReleaseArchiveRequest(
            staging,
            archivePath,
            manifestPath,
            "Sockseek.Desktop.exe",
            "Sockseek.Server.exe"));

        Assert.IsFalse(result.IsValid);
        CollectionAssert.Contains(result.Errors.ToArray(), "Archive path must not be inside the staging directory.");
        Assert.IsFalse(File.Exists(archivePath));
    }

    private static string CreateValidWindowsStaging(TempDirectory temp)
    {
        string repoRoot = Path.Combine(temp.Path, "repo");
        string desktopPublish = Path.Combine(temp.Path, "desktop");
        string daemonPublish = Path.Combine(temp.Path, "daemon");
        string staging = Path.Combine(temp.Path, "staging");
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(desktopPublish);
        Directory.CreateDirectory(daemonPublish);
        WriteRequiredRepoArtifacts(repoRoot);
        File.WriteAllText(Path.Combine(desktopPublish, "Sockseek.Desktop.exe"), "desktop");
        File.WriteAllText(Path.Combine(daemonPublish, "Sockseek.Server.exe"), "daemon");
        string sbomPath = Path.Combine(temp.Path, "sbom.spdx.json");
        WriteValidSbom(sbomPath);

        var result = ReleaseStager.StageWindows(new ReleaseStageRequest(
            repoRoot,
            desktopPublish,
            daemonPublish,
            staging,
            "Sockseek.Desktop.exe",
            "Sockseek.Server.exe",
            "3.0.5",
            "abc123",
            "https://github.com/k33zo33/sockseek",
            sbomPath));
        Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        return staging;
    }

    private static void WriteValidSbom(string path)
    {
        File.WriteAllText(
            path,
            """
            {
              "spdxVersion": "SPDX-2.3",
              "SPDXID": "SPDXRef-DOCUMENT",
              "name": "Sockseek",
              "packages": [
                {
                  "SPDXID": "SPDXRef-Package-Sockseek",
                  "name": "Sockseek"
                }
              ]
            }
            """);
    }

    private static void WriteRequiredRepoArtifacts(string repoRoot)
    {
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs"));
        File.WriteAllText(Path.Combine(repoRoot, ReleaseArtifactValidator.LicenseFileName), "GNU AGPL-3.0");
        File.WriteAllText(Path.Combine(repoRoot, ReleaseArtifactValidator.ThirdPartyNoticesFileName), "notices");
        File.WriteAllText(Path.Combine(repoRoot, ReleaseArtifactValidator.SecurityFileName), "security");
        File.WriteAllText(Path.Combine(repoRoot, ReleaseArtifactValidator.BetaLimitationsFileName), "beta limitations");
    }

    private static void RunPowerShellScript(string scriptPath, params string[] arguments)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo("powershell.exe")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = System.Diagnostics.Process.Start(startInfo);
        Assert.IsNotNull(process);
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.AreEqual(0, process.ExitCode, output + Environment.NewLine + error);
    }

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Sockseek-packager-stage-test-" + Guid.NewGuid());
            Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
