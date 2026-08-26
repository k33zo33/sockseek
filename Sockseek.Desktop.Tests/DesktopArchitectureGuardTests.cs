using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopArchitectureGuardTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
    private static readonly string DesktopProjectFile = Path.Combine(RepositoryRoot, "Sockseek.Desktop", "Sockseek.Desktop.csproj");
    private static readonly string DesktopSourceDirectory = Path.Combine(RepositoryRoot, "Sockseek.Desktop");

    [TestMethod]
    public void DesktopAssembly_DoesNotReferenceSockseekCore()
    {
        var references = typeof(DesktopShellSession).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

        CollectionAssert.DoesNotContain(references, "Sockseek.Core");
    }

    [TestMethod]
    public void DesktopProjectFile_DoesNotContainDirectSockseekCoreReference()
    {
        var projectFile = File.ReadAllText(DesktopProjectFile);

        Assert.IsFalse(projectFile.Contains("Sockseek.Core.csproj", StringComparison.Ordinal), "Desktop project must not reference Sockseek.Core.csproj.");
        Assert.IsFalse(projectFile.Contains("Include=\"Sockseek.Core\"", StringComparison.Ordinal), "Desktop project must not include a direct Sockseek.Core reference.");
    }

    [TestMethod]
    public void DesktopSource_DoesNotUseDbContextOrEntityFramework()
    {
        var sourceFiles = Directory
            .EnumerateFiles(DesktopSourceDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .ToArray();

        Assert.IsTrue(sourceFiles.Length > 0, "Expected desktop source files to be present.");

        foreach (var sourceFile in sourceFiles)
        {
            var contents = File.ReadAllText(sourceFile);
            Assert.IsFalse(contents.Contains("DbContext", StringComparison.Ordinal), $"Unexpected DbContext usage in {sourceFile}.");
            Assert.IsFalse(contents.Contains("Microsoft.EntityFrameworkCore", StringComparison.Ordinal), $"Unexpected EF Core usage in {sourceFile}.");
        }
    }
}
