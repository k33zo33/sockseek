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
        Assert.AreEqual(DesktopDesignTokens.Icon.Home, viewModel.CurrentPage.IconToken);
        Assert.AreEqual(DesktopDesignTokens.Surface.Page, viewModel.CurrentPage.SurfaceToken);
        Assert.AreEqual(DesktopThemePreference.System, viewModel.CurrentTheme);
        Assert.AreEqual(BackendConnectionState.Starting, viewModel.BackendState);
        Assert.IsTrue(viewModel.StatusBanner.IsVisible);
        Assert.AreEqual(DesktopDesignTokens.Surface.BannerInfo, viewModel.StatusBanner.SurfaceToken);
        Assert.AreEqual(DesktopDesignTokens.Icon.BannerInfo, viewModel.StatusBanner.IconToken);
        Assert.AreEqual("Shell.Backend.Starting.Title", viewModel.StatusBanner.TitleResourceKey);
        Assert.AreEqual("Shell.Backend.Starting.Message", viewModel.StatusBanner.MessageResourceKey);
        Assert.AreEqual("Shell.Backend.Starting.IconLabel", viewModel.StatusBanner.IconAccessibilityLabelResourceKey);
        Assert.AreEqual("Backend starting status", viewModel.StatusBanner.IconAccessibilityLabel);
        Assert.IsFalse(viewModel.CommandPalette.IsOpen);
        Assert.AreEqual(DesktopDesignTokens.Surface.CommandPalette, viewModel.CommandPalette.SurfaceToken);
        CollectionAssert.AreEqual(
            Enum.GetValues<ShellSection>(),
            viewModel.Items.Select(item => item.Section).ToArray());
        CollectionAssert.AreEqual(
            Enum.GetValues<ShellSection>().Select(GetExpectedIconToken).ToArray(),
            viewModel.Items.Select(item => item.IconToken).ToArray());
        CollectionAssert.AreEqual(
            Enum.GetValues<ShellSection>().Select(GetExpectedTitleResourceKey).ToArray(),
            viewModel.Items.Select(item => item.DisplayNameResourceKey).ToArray());
        CollectionAssert.AreEqual(
            Enum.GetValues<ShellSection>().Select(GetExpectedShortcut).ToArray(),
            viewModel.Items.Select(item => item.Shortcut).ToArray());
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
        Assert.AreEqual(GetExpectedShortcut(expectedSection), viewModel.Items.Single(item => item.Section == expectedSection).Shortcut);
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
        Assert.AreEqual(DesktopDesignTokens.Typography.CommandPaletteTitle, viewModel.CommandPalette.TitleTypographyToken);
        Assert.AreEqual(DesktopDesignTokens.Typography.CommandPaletteItem, viewModel.CommandPalette.ItemTypographyToken);
        Assert.AreEqual("Shell.CommandPalette.Placeholder", viewModel.CommandPalette.PlaceholderResourceKey);
        Assert.IsTrue(viewModel.CommandPalette.TryGetItem("navigate-search", out var item));
        Assert.AreEqual("Shell.CommandPalette.NavigateSearch", item?.TitleResourceKey);
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
        Assert.AreEqual("Shell.PlayerBar.Title", viewModel.PlayerBar.TitleResourceKey);
        Assert.AreEqual("Shell.PlayerBar.Artist", viewModel.PlayerBar.ArtistResourceKey);
        Assert.AreEqual("Shell.PlayerBar.QueueSummary", viewModel.PlayerBar.QueueSummaryResourceKey);
        Assert.AreEqual(DesktopDesignTokens.Surface.PlayerBar, viewModel.PlayerBar.SurfaceToken);
        Assert.AreEqual(DesktopDesignTokens.Icon.PlayerQueue, viewModel.PlayerBar.QueueIconToken);
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
        Assert.AreEqual(GetExpectedIconToken(section), viewModel.CurrentPage.IconToken);
        var navigationItem = viewModel.Items.Single(item => item.Section == section);
        Assert.AreEqual(GetExpectedTitleResourceKey(section), navigationItem.DisplayNameResourceKey);
        Assert.AreEqual(GetExpectedHintResourceKey(section), navigationItem.HintResourceKey);
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
        Assert.AreEqual(GetExpectedBannerSurfaceToken(state), viewModel.StatusBanner.SurfaceToken);
        Assert.AreEqual(GetExpectedBannerTitleResourceKey(state), viewModel.StatusBanner.TitleResourceKey);
        Assert.AreEqual(GetExpectedBannerIconLabelResourceKey(state), viewModel.StatusBanner.IconAccessibilityLabelResourceKey);
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

    private static string GetExpectedIconToken(ShellSection section)
        => section switch
        {
            ShellSection.Home => DesktopDesignTokens.Icon.Home,
            ShellSection.Search => DesktopDesignTokens.Icon.Search,
            ShellSection.Playlists => DesktopDesignTokens.Icon.Playlists,
            ShellSection.Library => DesktopDesignTokens.Icon.Library,
            ShellSection.Downloads => DesktopDesignTokens.Icon.Downloads,
            ShellSection.Accounts => DesktopDesignTokens.Icon.Accounts,
            ShellSection.Settings => DesktopDesignTokens.Icon.Settings,
            _ => throw new ArgumentOutOfRangeException(nameof(section), section, null)
        };

    private static string GetExpectedTitleResourceKey(ShellSection section)
        => section switch
        {
            ShellSection.Home => "Shell.Home.Title",
            ShellSection.Search => "Shell.Search.Title",
            ShellSection.Playlists => "Shell.Playlists.Title",
            ShellSection.Library => "Shell.Library.Title",
            ShellSection.Downloads => "Shell.Downloads.Title",
            ShellSection.Accounts => "Shell.Accounts.Title",
            ShellSection.Settings => "Shell.Settings.Title",
            _ => throw new ArgumentOutOfRangeException(nameof(section), section, null)
        };

    private static string GetExpectedShortcut(ShellSection section)
        => section switch
        {
            ShellSection.Home => "Ctrl+1",
            ShellSection.Search => "Ctrl+L",
            ShellSection.Playlists => "Ctrl+2",
            ShellSection.Library => "Ctrl+3",
            ShellSection.Downloads => "Ctrl+4",
            ShellSection.Accounts => "Ctrl+5",
            ShellSection.Settings => "Ctrl+,",
            _ => throw new ArgumentOutOfRangeException(nameof(section), section, null)
        };

    private static string GetExpectedHintResourceKey(ShellSection section)
        => section switch
        {
            ShellSection.Home => "Shell.Navigation.Home.Hint",
            ShellSection.Search => "Shell.Navigation.Search.Hint",
            ShellSection.Playlists => "Shell.Navigation.Playlists.Hint",
            ShellSection.Library => "Shell.Navigation.Library.Hint",
            ShellSection.Downloads => "Shell.Navigation.Downloads.Hint",
            ShellSection.Accounts => "Shell.Navigation.Accounts.Hint",
            ShellSection.Settings => "Shell.Navigation.Settings.Hint",
            _ => throw new ArgumentOutOfRangeException(nameof(section), section, null)
        };

    private static string GetExpectedBannerSurfaceToken(BackendConnectionState state)
        => state switch
        {
            BackendConnectionState.Starting => DesktopDesignTokens.Surface.BannerInfo,
            BackendConnectionState.Connected => DesktopDesignTokens.Surface.BannerSuccess,
            BackendConnectionState.Restarting => DesktopDesignTokens.Surface.BannerWarning,
            BackendConnectionState.Disconnected => DesktopDesignTokens.Surface.BannerDanger,
            BackendConnectionState.Unauthorized => DesktopDesignTokens.Surface.BannerDanger,
            _ => DesktopDesignTokens.Surface.BannerWarning
        };

    private static string GetExpectedBannerTitleResourceKey(BackendConnectionState state)
        => state switch
        {
            BackendConnectionState.Starting => "Shell.Backend.Starting.Title",
            BackendConnectionState.Connected => "Shell.Backend.Connected.Title",
            BackendConnectionState.Restarting => "Shell.Backend.Restarting.Title",
            BackendConnectionState.Disconnected => "Shell.Backend.Disconnected.Title",
            BackendConnectionState.Unauthorized => "Shell.Backend.Unauthorized.Title",
            _ => "Shell.Backend.Unknown.Title"
        };

    private static string GetExpectedBannerIconLabelResourceKey(BackendConnectionState state)
        => state switch
        {
            BackendConnectionState.Starting => "Shell.Backend.Starting.IconLabel",
            BackendConnectionState.Connected => "Shell.Backend.Connected.IconLabel",
            BackendConnectionState.Restarting => "Shell.Backend.Restarting.IconLabel",
            BackendConnectionState.Disconnected => "Shell.Backend.Disconnected.IconLabel",
            BackendConnectionState.Unauthorized => "Shell.Backend.Unauthorized.IconLabel",
            _ => "Shell.Backend.Unknown.IconLabel"
        };
}
