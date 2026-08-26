using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Architecture;

[TestClass]
public class ProjectDependencyRulesTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));

    [TestMethod]
    public void Domain_DoesNotReferencePackagesOrForbiddenProjects()
    {
        var project = LoadProject("Sockseek.Domain/Sockseek.Domain.csproj");

        Assert.AreEqual(0, PackageReferences(project).Length, "Sockseek.Domain should not reference NuGet packages.");
        Assert.AreEqual(0, ProjectReferences(project).Length, "Sockseek.Domain should not reference other projects.");
    }

    [TestMethod]
    public void Desktop_DoesNotReferenceCore()
    {
        var project = LoadProject("Sockseek.Desktop/Sockseek.Desktop.csproj");
        var references = ProjectReferences(project);

        CollectionAssert.DoesNotContain(references, "..\\Sockseek.Core\\Sockseek.Core.csproj");
        CollectionAssert.Contains(references, "..\\Sockseek.Api\\Sockseek.Api.csproj");
    }

    [TestMethod]
    public void Application_ReferencesOnlyDomainAndIntegrationAbstractions()
    {
        var project = LoadProject("Sockseek.Application/Sockseek.Application.csproj");
        CollectionAssert.AreEquivalent(
            new[]
            {
                "..\\Sockseek.Domain\\Sockseek.Domain.csproj",
                "..\\Sockseek.Integrations.Abstractions\\Sockseek.Integrations.Abstractions.csproj",
            },
            ProjectReferences(project));
    }

    [TestMethod]
    public void Infrastructure_ReferencesApplicationAndDomainOnly()
    {
        var project = LoadProject("Sockseek.Infrastructure/Sockseek.Infrastructure.csproj");
        CollectionAssert.AreEquivalent(
            new[]
            {
                "..\\Sockseek.Application\\Sockseek.Application.csproj",
                "..\\Sockseek.Domain\\Sockseek.Domain.csproj",
            },
            ProjectReferences(project));
    }

    [TestMethod]
    public void Player_ReferencesApplicationAndDomainOnly()
    {
        var project = LoadProject("Sockseek.Player/Sockseek.Player.csproj");
        CollectionAssert.AreEquivalent(
            new[]
            {
                "..\\Sockseek.Application\\Sockseek.Application.csproj",
                "..\\Sockseek.Domain\\Sockseek.Domain.csproj",
            },
            ProjectReferences(project));
    }

    private static XDocument LoadProject(string relativePath)
        => XDocument.Load(Path.Combine(RepoRoot, relativePath));

    private static string[] ProjectReferences(XDocument document)
        => document.Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>()
            .ToArray();

    private static string[] PackageReferences(XDocument document)
        => document.Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>()
            .ToArray();
}
