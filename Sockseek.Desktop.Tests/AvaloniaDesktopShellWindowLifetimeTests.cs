using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class AvaloniaDesktopShellWindowLifetimeTests
{
    private static readonly string LifetimeSourcePath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "../../../../Sockseek.Desktop/AvaloniaDesktopShellWindowLifetime.cs"));

    [TestMethod]
    public void MainWindow_IsCreatedAfterAvaloniaPlatformSetup()
    {
        var source = File.ReadAllText(LifetimeSourcePath);
        var afterSetupIndex = source.IndexOf(".AfterSetup(", StringComparison.Ordinal);
        var mainWindowIndex = source.IndexOf("new DesktopShellMainWindow", StringComparison.Ordinal);
        var lifetimeStartIndex = source.IndexOf(".StartWithClassicDesktopLifetime(", StringComparison.Ordinal);

        Assert.IsTrue(afterSetupIndex >= 0, "Expected the Avalonia app builder to use AfterSetup.");
        Assert.IsTrue(
            mainWindowIndex > afterSetupIndex,
            "DesktopShellMainWindow must not be constructed before Avalonia platform setup.");
        Assert.IsTrue(
            lifetimeStartIndex > mainWindowIndex,
            "DesktopShellMainWindow must be constructed by the AfterSetup callback before the lifetime starts.");
    }

    [TestMethod]
    public void RequiresDedicatedStaThread_OnWindows_OnlyForNonStaCaller()
    {
        Assert.IsFalse(AvaloniaDesktopShellWindowLifetime.RequiresDedicatedStaThread(
            isWindows: true,
            ApartmentState.STA));
        Assert.IsTrue(AvaloniaDesktopShellWindowLifetime.RequiresDedicatedStaThread(
            isWindows: true,
            ApartmentState.MTA));
        Assert.IsTrue(AvaloniaDesktopShellWindowLifetime.RequiresDedicatedStaThread(
            isWindows: true,
            ApartmentState.Unknown));
        Assert.IsFalse(AvaloniaDesktopShellWindowLifetime.RequiresDedicatedStaThread(
            isWindows: false,
            ApartmentState.MTA));
    }
}
