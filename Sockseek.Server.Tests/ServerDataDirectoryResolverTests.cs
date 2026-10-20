using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Server;

namespace Tests.Server;

[TestClass]
public sealed class ServerDataDirectoryResolverTests
{
    [TestMethod]
    public void ResolveDefault_WhenLocalApplicationDataIsEmpty_UsesAbsoluteXdgDataHome()
    {
        string xdgDataHome = Path.Combine(Path.GetTempPath(), "sockseek-xdg-data");

        string result = ServerDataDirectoryResolver.ResolveDefault(
            localApplicationData: null,
            xdgDataHome,
            userProfile: null,
            homeEnvironment: null);

        Assert.AreEqual(Path.GetFullPath(Path.Combine(xdgDataHome, "Sockseek")), result);
    }

    [TestMethod]
    public void ResolveDefault_WhenSpecialFolderAndXdgAreUnavailable_UsesHomeFallback()
    {
        string home = Path.Combine(Path.GetTempPath(), "sockseek-home");

        string result = ServerDataDirectoryResolver.ResolveDefault(
            localApplicationData: string.Empty,
            xdgDataHome: "relative-xdg",
            userProfile: string.Empty,
            homeEnvironment: home);

        Assert.AreEqual(
            Path.GetFullPath(Path.Combine(home, ".local", "share", "Sockseek")),
            result);
    }

    [TestMethod]
    public void ResolveDefault_WhenNoAbsoluteUserDirectoryExists_ThrowsActionableError()
    {
        var exception = Assert.ThrowsException<InvalidOperationException>(() =>
            ServerDataDirectoryResolver.ResolveDefault(
                localApplicationData: null,
                xdgDataHome: "relative-xdg",
                userProfile: null,
                homeEnvironment: "relative-home"));

        StringAssert.Contains(exception.Message, "SockseekServer__ConfigDir");
    }
}
