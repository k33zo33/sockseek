using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Packager;

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

        File.WriteAllText(Path.Combine(repoRoot, ReleaseArtifactValidator.LicenseFileName), "GNU AGPL-3.0");
        File.WriteAllText(Path.Combine(repoRoot, ReleaseArtifactValidator.ThirdPartyNoticesFileName), "notices");
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
        File.WriteAllText(Path.Combine(repoRoot, ReleaseArtifactValidator.LicenseFileName), "GNU AGPL-3.0");
        File.WriteAllText(Path.Combine(repoRoot, ReleaseArtifactValidator.ThirdPartyNoticesFileName), "notices");
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
