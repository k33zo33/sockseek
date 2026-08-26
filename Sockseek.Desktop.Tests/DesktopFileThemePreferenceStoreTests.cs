using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopFileThemePreferenceStoreTests
{
    [TestMethod]
    public void Load_WhenFileDoesNotExist_ReturnsSystem()
    {
        using var tempDirectory = new TemporaryDirectory();
        var store = new DesktopFileThemePreferenceStore(Path.Combine(tempDirectory.Path, "theme.json"));

        var preference = store.Load();

        Assert.AreEqual(DesktopThemePreference.System, preference);
    }

    [TestMethod]
    public void Save_ThenLoad_RoundTripsThemePreference()
    {
        using var tempDirectory = new TemporaryDirectory();
        var store = new DesktopFileThemePreferenceStore(Path.Combine(tempDirectory.Path, "theme.json"));

        store.Save(DesktopThemePreference.Dark);

        Assert.AreEqual(DesktopThemePreference.Dark, store.Load());
    }

    [TestMethod]
    public void Load_WithInvalidJson_FallsBackToSystem()
    {
        using var tempDirectory = new TemporaryDirectory();
        var filePath = Path.Combine(tempDirectory.Path, "theme.json");
        File.WriteAllText(filePath, "not-json");
        var store = new DesktopFileThemePreferenceStore(filePath);

        var preference = store.Load();

        Assert.AreEqual(DesktopThemePreference.System, preference);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sockseek-desktop-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
