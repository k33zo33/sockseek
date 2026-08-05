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
        Assert.AreEqual("Shell.Home.Title", viewModel.CurrentPage.TitleResourceKey);
        Assert.AreEqual(DesktopThemePreference.System, viewModel.CurrentTheme);
        Assert.AreEqual(BackendConnectionState.Starting, viewModel.BackendState);
        Assert.IsTrue(viewModel.StatusBanner.IsVisible);
        Assert.IsFalse(viewModel.CommandPalette.IsOpen);
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
        Assert.IsFalse(viewModel.CommandPalette.IsOpen);
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
    public void TryHandleShortcut_CommandPaletteShortcut_OpensCommandPalette()
    {
        var viewModel = new ShellNavigationViewModel();

        var handled = viewModel.TryHandleShortcut("Ctrl+K");

        Assert.IsTrue(handled);
        Assert.IsTrue(viewModel.CommandPalette.IsOpen);
        Assert.AreEqual("Shell.CommandPalette.Title", viewModel.CommandPalette.TitleResourceKey);
        Assert.AreEqual(7, viewModel.CommandPalette.Items.Count);
        Assert.IsTrue(viewModel.CommandPalette.TryGetItem("navigate-search", out var item));
        Assert.AreEqual(ShellSection.Search, item?.TargetSection);
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

    [DataTestMethod]
    [DataRow(BackendConnectionState.Starting, true, "Starting local daemon")]
    [DataRow(BackendConnectionState.Connected, false, "Connected")]
    [DataRow(BackendConnectionState.Restarting, true, "Restarting local daemon")]
    [DataRow(BackendConnectionState.Disconnected, true, "Backend disconnected")]
    [DataRow(BackendConnectionState.Unauthorized, true, "Session expired")]
    public void SetBackendState_UpdatesBannerForExpectedUxState(BackendConnectionState state, bool visible, string expectedTitle)
    {
        var viewModel = new ShellNavigationViewModel();

        viewModel.SetBackendState(state);

        Assert.AreEqual(state, viewModel.BackendState);
        Assert.AreEqual(state, viewModel.StatusBanner.State);
        Assert.AreEqual(visible, viewModel.StatusBanner.IsVisible);
        Assert.AreEqual(expectedTitle, viewModel.StatusBanner.Title);
    }

    [TestMethod]
    public void Constructor_WithSupervisor_UsesExistingSupervisorSnapshot()
    {
        var supervisor = new DesktopDaemonSupervisor();
        supervisor.TryAcceptHandshakePayload("{\"BaseUrl\":\"http://127.0.0.1:5030\",\"SessionToken\":\"shell-token\"}");

        var viewModel = new ShellNavigationViewModel(supervisor);

        Assert.AreEqual(BackendConnectionState.Connected, viewModel.BackendState);
        Assert.IsNotNull(viewModel.CurrentHandshake);
        Assert.AreEqual("shell-token", viewModel.CurrentHandshake.SessionToken);
        Assert.IsFalse(viewModel.StatusBanner.IsVisible);
    }

    [TestMethod]
    public void Constructor_WithSupervisor_TracksFutureSupervisorStateChanges()
    {
        var supervisor = new DesktopDaemonSupervisor();
        var viewModel = new ShellNavigationViewModel(supervisor);

        supervisor.TryAcceptHandshakePayload("{\"BaseUrl\":\"http://localhost:5030\",\"SessionToken\":\"shell-token\"}");
        Assert.AreEqual(BackendConnectionState.Connected, viewModel.BackendState);
        Assert.IsNotNull(viewModel.CurrentHandshake);

        supervisor.MarkRestarting();
        Assert.AreEqual(BackendConnectionState.Restarting, viewModel.BackendState);
        Assert.IsNull(viewModel.CurrentHandshake);

        supervisor.MarkUnauthorized();
        Assert.AreEqual(BackendConnectionState.Unauthorized, viewModel.BackendState);
        Assert.AreEqual("Session expired", viewModel.StatusBanner.Title);
    }

    [TestMethod]
    public void Constructor_UsesStoredThemePreference_AndPersistsUpdates()
    {
        var store = new InMemoryDesktopThemePreferenceStore(DesktopThemePreference.Dark);
        var firstViewModel = new ShellNavigationViewModel(themePreferenceStore: store);

        Assert.AreEqual(DesktopThemePreference.Dark, firstViewModel.CurrentTheme);

        firstViewModel.SetTheme(DesktopThemePreference.Light);
        var secondViewModel = new ShellNavigationViewModel(themePreferenceStore: store);

        Assert.AreEqual(DesktopThemePreference.Light, firstViewModel.CurrentTheme);
        Assert.AreEqual(DesktopThemePreference.Light, secondViewModel.CurrentTheme);
    }
}
