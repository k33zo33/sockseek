using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public class ShellNavigationViewModelTests
{
    [TestMethod]
    public void Constructor_DefaultsToHomeAndListsAllSections()
    {
        var viewModel = new ShellNavigationViewModel();

        Assert.AreEqual(ShellSection.Home, viewModel.CurrentSection);
        Assert.AreEqual(ShellSection.Home, viewModel.CurrentPage.Section);
        Assert.AreEqual("Home", viewModel.CurrentPage.Title);
        CollectionAssert.AreEqual(
            Enum.GetValues<ShellSection>(),
            viewModel.Items.Select(item => item.Section).ToArray());
    }

    [DataTestMethod]
    [DataRow("Ctrl+1", ShellSection.Home)]
    [DataRow("Ctrl+L", ShellSection.Search)]
    [DataRow("Ctrl+2", ShellSection.Playlists)]
    [DataRow("Ctrl+3", ShellSection.Library)]
    [DataRow("Ctrl+4", ShellSection.Downloads)]
    [DataRow("Ctrl+5", ShellSection.Accounts)]
    [DataRow("Ctrl+,", ShellSection.Settings)]
    public void TryHandleShortcut_KnownShortcut_NavigatesToExpectedSection(string shortcut, ShellSection expectedSection)
    {
        var viewModel = new ShellNavigationViewModel();

        var handled = viewModel.TryHandleShortcut(shortcut);

        Assert.IsTrue(handled);
        Assert.AreEqual(expectedSection, viewModel.CurrentSection);
        Assert.AreEqual(expectedSection, viewModel.CurrentPage.Section);
    }

    [TestMethod]
    public void TryHandleShortcut_UnknownShortcut_DoesNotChangeSection()
    {
        var viewModel = new ShellNavigationViewModel();

        var handled = viewModel.TryHandleShortcut("Ctrl+Shift+X");

        Assert.IsFalse(handled);
        Assert.AreEqual(ShellSection.Home, viewModel.CurrentSection);
        Assert.AreEqual(ShellSection.Home, viewModel.CurrentPage.Section);
    }

    [TestMethod]
    public void Constructor_ProvidesPersistentPlayerPlaceholder()
    {
        var viewModel = new ShellNavigationViewModel();

        Assert.AreEqual("Nothing playing", viewModel.PlayerBar.Title);
        Assert.AreEqual("Choose a local track or completed download", viewModel.PlayerBar.Artist);
        Assert.IsFalse(viewModel.PlayerBar.CanPlayPause);
        Assert.IsFalse(viewModel.PlayerBar.CanGoPrevious);
        Assert.IsFalse(viewModel.PlayerBar.CanGoNext);
    }

    [DataTestMethod]
    [DataRow(ShellSection.Home, "Backend status, recent activity, and onboarding live here.")]
    [DataRow(ShellSection.Search, "Track and album search UI will appear here.")]
    [DataRow(ShellSection.Playlists, "Imported playlists and resolution progress will appear here.")]
    [DataRow(ShellSection.Library, "Local library browsing and scans will appear here.")]
    [DataRow(ShellSection.Downloads, "Active and completed download workflows will appear here.")]
    [DataRow(ShellSection.Accounts, "Provider connections and authorization status will appear here.")]
    [DataRow(ShellSection.Settings, "Theme, daemon, and library preferences will appear here.")]
    public void NavigateTo_AllPrimarySections_ExposesExpectedPlaceholderPage(ShellSection section, string expectedDescription)
    {
        var viewModel = new ShellNavigationViewModel();

        viewModel.NavigateTo(section);

        Assert.AreEqual(section, viewModel.CurrentPage.Section);
        Assert.AreEqual(expectedDescription, viewModel.CurrentPage.Description);
    }
}
