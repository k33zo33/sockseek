using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class ShellNavigationHeadlessTests
{
    [TestMethod]
    public void CommandPaletteSelection_NavigatesToTargetSection_AndClosesPalette()
    {
        var viewModel = new ShellNavigationViewModel();
        viewModel.OpenCommandPalette();

        var handled = viewModel.TryExecuteCommandPaletteItem("navigate-settings");

        Assert.IsTrue(handled);
        Assert.AreEqual(ShellSection.Settings, viewModel.CurrentSection);
        Assert.AreEqual(ShellSection.Settings, viewModel.CurrentPage.Section);
        Assert.IsFalse(viewModel.CommandPalette.IsOpen);
    }

    [TestMethod]
    public void CommandPaletteSelection_UnknownItem_DoesNotChangeCurrentSection()
    {
        var viewModel = new ShellNavigationViewModel();
        viewModel.NavigateTo(ShellSection.Library);
        viewModel.OpenCommandPalette();

        var handled = viewModel.TryExecuteCommandPaletteItem("missing-command");

        Assert.IsFalse(handled);
        Assert.AreEqual(ShellSection.Library, viewModel.CurrentSection);
        Assert.IsTrue(viewModel.CommandPalette.IsOpen);
    }

    [TestMethod]
    public void CommandPaletteShortcut_TogglesPaletteOpenAndClosed()
    {
        var viewModel = new ShellNavigationViewModel();

        var opened = viewModel.TryHandleShortcut("Ctrl+K");
        var closed = viewModel.TryHandleShortcut("Ctrl+K");

        Assert.IsTrue(opened);
        Assert.IsTrue(closed);
        Assert.IsFalse(viewModel.CommandPalette.IsOpen);
    }

    [TestMethod]
    public void DirectNavigation_ClosesOpenCommandPalette()
    {
        var viewModel = new ShellNavigationViewModel();
        viewModel.OpenCommandPalette();

        viewModel.NavigateTo(ShellSection.Downloads);

        Assert.AreEqual(ShellSection.Downloads, viewModel.CurrentSection);
        Assert.IsFalse(viewModel.CommandPalette.IsOpen);
    }

    [TestMethod]
    public void ShortcutThenCommandPaletteSelection_ProducesExpectedHeadlessNavigationFlow()
    {
        var viewModel = new ShellNavigationViewModel();

        var openedPalette = viewModel.TryHandleShortcut("Ctrl+K");
        var selectedCommand = viewModel.TryExecuteCommandPaletteItem("navigate-downloads");

        Assert.IsTrue(openedPalette);
        Assert.IsTrue(selectedCommand);
        Assert.AreEqual(ShellSection.Downloads, viewModel.CurrentSection);
        Assert.AreEqual("Downloads", viewModel.CurrentPage.Title);
        Assert.IsFalse(viewModel.CommandPalette.IsOpen);
    }
}
