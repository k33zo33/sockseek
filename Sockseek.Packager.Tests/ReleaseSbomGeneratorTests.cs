using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Packager;

namespace Tests.Packager;

[TestClass]
public sealed class ReleaseSbomGeneratorTests
{
    [TestMethod]
    public void Generate_WithPackagesLockFiles_WritesSpdxPackageInventory()
    {
        using var temp = TempDirectory.Create();
        string projectA = Path.Combine(temp.Path, "ProjectA");
        string projectB = Path.Combine(temp.Path, "ProjectB");
        Directory.CreateDirectory(projectA);
        Directory.CreateDirectory(projectB);
        WritePackagesLock(
            Path.Combine(projectA, "packages.lock.json"),
            ("AngleSharp", "1.8.2"),
            ("Newtonsoft.Json", "13.0.4"));
        WritePackagesLock(
            Path.Combine(projectB, "packages.lock.json"),
            ("AngleSharp", "1.8.2"),
            ("SQLitePCLRaw.lib.e_sqlite3", "2.1.13"));
        string outputPath = Path.Combine(temp.Path, "sbom.spdx.json");

        var result = ReleaseSbomGenerator.Generate(new ReleaseSbomGenerationRequest(
            temp.Path,
            outputPath,
            "Sockseek",
            "3.0.5",
            "abc123",
            "https://github.com/k33zo33/sockseek"));

        Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        Assert.AreEqual(3, result.PackageCount);
        using var document = JsonDocument.Parse(File.ReadAllText(outputPath));
        var root = document.RootElement;
        Assert.AreEqual("SPDX-2.3", root.GetProperty("spdxVersion").GetString());
        Assert.AreEqual("SPDXRef-DOCUMENT", root.GetProperty("SPDXID").GetString());
        Assert.AreEqual("Sockseek", root.GetProperty("name").GetString());

        var packages = root.GetProperty("packages").EnumerateArray().ToArray();
        CollectionAssert.AreEquivalent(
            new[] { "AngleSharp", "Newtonsoft.Json", "SQLitePCLRaw.lib.e_sqlite3" },
            packages.Select(package => package.GetProperty("name").GetString()).ToArray());
        Assert.IsTrue(packages.All(package => package.GetProperty("filesAnalyzed").GetBoolean() == false));
    }

    [TestMethod]
    public void Generate_IgnoresBuildOutputLockFiles()
    {
        using var temp = TempDirectory.Create();
        string project = Path.Combine(temp.Path, "Project");
        string obj = Path.Combine(project, "obj");
        Directory.CreateDirectory(project);
        Directory.CreateDirectory(obj);
        WritePackagesLock(Path.Combine(project, "packages.lock.json"), ("AngleSharp", "1.8.2"));
        WritePackagesLock(Path.Combine(obj, "packages.lock.json"), ("Ignored.Package", "1.0.0"));
        string outputPath = Path.Combine(temp.Path, "sbom.spdx.json");

        var result = ReleaseSbomGenerator.Generate(new ReleaseSbomGenerationRequest(
            temp.Path,
            outputPath,
            "Sockseek",
            "3.0.5",
            "abc123",
            "https://github.com/k33zo33/sockseek"));

        Assert.IsTrue(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        Assert.AreEqual(1, result.PackageCount);
        StringAssert.Contains(File.ReadAllText(outputPath), "AngleSharp");
        Assert.IsFalse(File.ReadAllText(outputPath).Contains("Ignored.Package", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_WhenRepositoryHasNoPackages_ReturnsActionableError()
    {
        using var temp = TempDirectory.Create();

        var result = ReleaseSbomGenerator.Generate(new ReleaseSbomGenerationRequest(
            temp.Path,
            Path.Combine(temp.Path, "sbom.spdx.json"),
            "Sockseek",
            "3.0.5",
            "abc123",
            "https://github.com/k33zo33/sockseek"));

        Assert.IsFalse(result.IsValid);
        CollectionAssert.Contains(result.Errors.ToArray(), "No resolved NuGet packages were found in packages.lock.json files.");
    }

    private static void WritePackagesLock(string path, params (string Name, string Version)[] packages)
    {
        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteNumber("version", 2);
        writer.WriteStartObject("dependencies");
        writer.WriteStartObject("net10.0");
        foreach (var package in packages)
        {
            writer.WriteStartObject(package.Name);
            writer.WriteString("type", "Direct");
            writer.WriteString("requested", "[" + package.Version + ", )");
            writer.WriteString("resolved", package.Version);
            writer.WriteString("contentHash", "hash");
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
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
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Sockseek-packager-sbom-test-" + Guid.NewGuid());
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
