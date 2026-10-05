using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Packager;

namespace Tests.Packager;

[TestClass]
public sealed class ReleaseArtifactValidatorTests
{
    [TestMethod]
    public void Validate_WithRequiredLegalAndMetadataArtifacts_Passes()
    {
        using var temp = TempDirectory.Create();
        File.WriteAllText(Path.Combine(temp.Path, "Sockseek.Desktop.exe"), "desktop");
        File.WriteAllText(Path.Combine(temp.Path, "Sockseek.Server.exe"), "daemon");
        File.WriteAllText(Path.Combine(temp.Path, ReleaseArtifactValidator.LicenseFileName), "GNU AGPL-3.0");
        File.WriteAllText(Path.Combine(temp.Path, ReleaseArtifactValidator.ThirdPartyNoticesFileName), "notices");
        WriteValidSbom(temp.Path);
        File.WriteAllText(
            Path.Combine(temp.Path, ReleaseArtifactValidator.MetadataFileName),
            """
            {
              "version": "3.0.5",
              "commit": "abc123",
              "sourceUrl": "https://github.com/k33zo33/sockseek",
              "license": "AGPL-3.0"
            }
            """);

        var result = ReleaseArtifactValidator.Validate(
            temp.Path,
            "Sockseek.Desktop.exe",
            "Sockseek.Server.exe");

        Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        Assert.AreEqual(0, result.Errors.Count);
    }

    [TestMethod]
    public void Validate_WhenReleaseGateArtifactsAreMissing_ReturnsActionableErrors()
    {
        using var temp = TempDirectory.Create();
        File.WriteAllText(
            Path.Combine(temp.Path, ReleaseArtifactValidator.MetadataFileName),
            """
            {
              "version": "",
              "commit": "",
              "sourceUrl": "not-a-url",
              "license": "MIT"
            }
            """);

        var result = ReleaseArtifactValidator.Validate(
            temp.Path,
            "Sockseek.Desktop.exe",
            "Sockseek.Server.exe");

        Assert.IsFalse(result.IsValid);
        CollectionAssert.Contains(result.Errors.ToArray(), "Missing required release artifact: LICENSE");
        CollectionAssert.Contains(result.Errors.ToArray(), "Missing required release artifact: THIRD-PARTY-NOTICES");
        CollectionAssert.Contains(result.Errors.ToArray(), "Missing required release artifact: Sockseek.Desktop.exe");
        CollectionAssert.Contains(result.Errors.ToArray(), "Missing required release artifact: Sockseek.Server.exe");
        CollectionAssert.Contains(result.Errors.ToArray(), "Missing required release artifact: sbom.spdx.json");
        CollectionAssert.Contains(result.Errors.ToArray(), "release-metadata.json must include 'version'.");
        CollectionAssert.Contains(result.Errors.ToArray(), "release-metadata.json must include 'commit'.");
        CollectionAssert.Contains(result.Errors.ToArray(), "release-metadata.json license must be AGPL-3.0.");
        CollectionAssert.Contains(result.Errors.ToArray(), "release-metadata.json sourceUrl must be an absolute HTTP(S) URL.");
    }

    [TestMethod]
    public void Validate_WhenExecutableNameEscapesStagingDirectory_ReturnsError()
    {
        using var temp = TempDirectory.Create();
        File.WriteAllText(Path.Combine(temp.Path, "Sockseek.Desktop.exe"), "desktop");
        File.WriteAllText(Path.Combine(temp.Path, "Sockseek.Server.exe"), "daemon");
        File.WriteAllText(Path.Combine(temp.Path, ReleaseArtifactValidator.LicenseFileName), "GNU AGPL-3.0");
        File.WriteAllText(Path.Combine(temp.Path, ReleaseArtifactValidator.ThirdPartyNoticesFileName), "notices");
        WriteValidSbom(temp.Path);
        File.WriteAllText(
            Path.Combine(temp.Path, ReleaseArtifactValidator.MetadataFileName),
            """
            {
              "version": "3.0.5",
              "commit": "abc123",
              "sourceUrl": "https://github.com/k33zo33/sockseek",
              "license": "AGPL-3.0"
            }
            """);

        var escapingDaemonName = Path.Combine("..", "Sockseek.Server.exe");
        var result = ReleaseArtifactValidator.Validate(
            temp.Path,
            "Sockseek.Desktop.exe",
            escapingDaemonName);

        Assert.IsFalse(result.IsValid);
        CollectionAssert.Contains(
            result.Errors.ToArray(),
            $"Release artifact path must stay inside staging directory: {escapingDaemonName}");
    }

    [TestMethod]
    public void Validate_WhenSbomIsInvalid_ReturnsActionableErrors()
    {
        using var temp = TempDirectory.Create();
        File.WriteAllText(Path.Combine(temp.Path, "Sockseek.Desktop.exe"), "desktop");
        File.WriteAllText(Path.Combine(temp.Path, "Sockseek.Server.exe"), "daemon");
        File.WriteAllText(Path.Combine(temp.Path, ReleaseArtifactValidator.LicenseFileName), "GNU AGPL-3.0");
        File.WriteAllText(Path.Combine(temp.Path, ReleaseArtifactValidator.ThirdPartyNoticesFileName), "notices");
        File.WriteAllText(
            Path.Combine(temp.Path, ReleaseArtifactValidator.MetadataFileName),
            """
            {
              "version": "3.0.5",
              "commit": "abc123",
              "sourceUrl": "https://github.com/k33zo33/sockseek",
              "license": "AGPL-3.0"
            }
            """);
        File.WriteAllText(
            Path.Combine(temp.Path, ReleaseArtifactValidator.SbomFileName),
            """
            {
              "spdxVersion": "",
              "SPDXID": "",
              "packages": []
            }
            """);

        var result = ReleaseArtifactValidator.Validate(
            temp.Path,
            "Sockseek.Desktop.exe",
            "Sockseek.Server.exe");

        Assert.IsFalse(result.IsValid);
        CollectionAssert.Contains(result.Errors.ToArray(), "sbom.spdx.json must include 'spdxVersion'.");
        CollectionAssert.Contains(result.Errors.ToArray(), "sbom.spdx.json must include 'SPDXID'.");
        CollectionAssert.Contains(result.Errors.ToArray(), "sbom.spdx.json must include 'name'.");
        CollectionAssert.Contains(result.Errors.ToArray(), "sbom.spdx.json must include at least one package.");
    }

    private static void WriteValidSbom(string directory)
    {
        File.WriteAllText(
            Path.Combine(directory, ReleaseArtifactValidator.SbomFileName),
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
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Sockseek-packager-test-" + Guid.NewGuid());
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
