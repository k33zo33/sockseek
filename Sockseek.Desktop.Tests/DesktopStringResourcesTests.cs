using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopStringResourcesTests
{
    [DataTestMethod]
    [DataRow("Shell.Home.Title", "Home")]
    [DataRow("Shell.CommandPalette.Title", "Command palette")]
    [DataRow("Shell.PlayerBar.Title", "Nothing playing")]
    [DataRow("Shell.PlayerBar.Artwork", "Artwork placeholder")]
    [DataRow("Shell.PlayerBar.Progress", "00:00 / --:--")]
    [DataRow("Shell.Backend.Starting.Title", "Starting local daemon")]
    public void Get_KnownResourceKey_ReturnsExpectedValue(string resourceKey, string expectedValue)
        => Assert.AreEqual(expectedValue, DesktopStringResources.Get(resourceKey));

    [TestMethod]
    public void Get_UnknownResourceKey_Throws()
        => Assert.ThrowsException<KeyNotFoundException>(() => DesktopStringResources.Get("Shell.Missing.Key"));
}
