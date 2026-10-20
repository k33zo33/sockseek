using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Desktop;

namespace Tests.Desktop;

[TestClass]
public sealed class DesktopSettingsPathsTests
{
    [TestMethod]
    public void ResolveLocalDataDirectory_WhenSpecialFolderIsEmpty_UsesAbsoluteXdgDataHome()
    {
        string xdgDataHome = Path.Combine(Path.GetTempPath(), "sockseek-desktop-xdg");

        string result = DesktopSettingsPaths.ResolveLocalDataDirectory(
            localApplicationData: null,
            xdgDataHome,
            userProfile: null,
            homeEnvironment: null);

        Assert.AreEqual(Path.GetFullPath(xdgDataHome), result);
    }

    [TestMethod]
    public void ResolveLocalDataDirectory_WhenSpecialFolderAndXdgAreUnavailable_UsesHomeFallback()
    {
        string home = Path.Combine(Path.GetTempPath(), "sockseek-desktop-home");

        string result = DesktopSettingsPaths.ResolveLocalDataDirectory(
            localApplicationData: string.Empty,
            xdgDataHome: "relative-xdg",
            userProfile: string.Empty,
            homeEnvironment: home);

        Assert.AreEqual(Path.GetFullPath(Path.Combine(home, ".local", "share")), result);
    }

    [TestMethod]
    public void ResolveLocalDataDirectory_WhenNoAbsoluteUserDirectoryExists_ThrowsActionableError()
    {
        var exception = Assert.ThrowsException<InvalidOperationException>(() =>
            DesktopSettingsPaths.ResolveLocalDataDirectory(
                localApplicationData: null,
                xdgDataHome: "relative-xdg",
                userProfile: null,
                homeEnvironment: "relative-home"));

        StringAssert.Contains(exception.Message, "XDG_DATA_HOME");
    }
}
