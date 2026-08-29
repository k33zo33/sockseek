using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopShellWindowViewModelTests
{
    [TestMethod]
    public async Task Constructor_ExposesDefaultShellChromeState()
    {
        await using var session = new DesktopShellSession(
            supervisor: new DesktopDaemonSupervisor(),
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake));
        var viewModel = new DesktopShellWindowViewModel(session);

        Assert.AreEqual("Shell.Window.Title", viewModel.TitleResourceKey);
        Assert.AreEqual("Sockseek", viewModel.Title);
        Assert.AreEqual("Sockseek — Home", viewModel.WindowTitle);
        Assert.AreEqual(DesktopDesignTokens.Surface.AppCanvas, viewModel.SurfaceToken);
        Assert.AreEqual(DesktopDesignTokens.Spacing.ShellChrome, viewModel.ChromeSpacingToken);
        Assert.AreSame(session.Shell, viewModel.Shell);
        CollectionAssert.AreEqual(session.Shell.Items.ToArray(), viewModel.NavigationItems.ToArray());
        Assert.AreEqual(session.Shell.Items.Count, viewModel.NavigationButtons.Count);
        Assert.AreEqual("• Home (Ctrl+1)", viewModel.NavigationButtons[0].DisplayLabel);
        Assert.AreEqual("Search (Ctrl+L)", viewModel.NavigationButtons[1].DisplayLabel);
        Assert.AreSame(session.Shell.PlayerBar, viewModel.PlayerBar);
        Assert.AreEqual("Nothing playing", viewModel.PlayerBarTitle);
        Assert.AreEqual("Shell.PlayerBar.Title", viewModel.PlayerBarTitleResourceKey);
        Assert.AreEqual("Artwork placeholder", viewModel.PlayerBarArtwork);
        Assert.AreEqual("Shell.PlayerBar.Artwork", viewModel.PlayerBarArtworkResourceKey);
        Assert.AreEqual("Choose a local track or completed download", viewModel.PlayerBarArtist);
        Assert.AreEqual("Shell.PlayerBar.Artist", viewModel.PlayerBarArtistResourceKey);
        Assert.AreEqual("00:00 / --:--", viewModel.PlayerBarProgress);
        Assert.AreEqual("Shell.PlayerBar.Progress", viewModel.PlayerBarProgressResourceKey);
        Assert.AreEqual("Queue unavailable until playback coordinator is connected", viewModel.PlayerBarQueueSummary);
        Assert.AreEqual("Shell.PlayerBar.QueueSummary", viewModel.PlayerBarQueueSummaryResourceKey);
        Assert.IsFalse(viewModel.CanGoPrevious);
        Assert.AreEqual("Previous track", viewModel.PreviousIconAccessibilityLabel);
        Assert.AreEqual("Shell.PlayerBar.Previous.IconLabel", viewModel.PreviousIconAccessibilityLabelResourceKey);
        Assert.AreEqual(DesktopDesignTokens.Icon.PlayerPrevious, viewModel.PreviousIconToken);
        Assert.IsFalse(viewModel.CanPlayPause);
        Assert.AreEqual("Play or pause", viewModel.PlayPauseIconAccessibilityLabel);
        Assert.AreEqual("Shell.PlayerBar.PlayPause.IconLabel", viewModel.PlayPauseIconAccessibilityLabelResourceKey);
        Assert.AreEqual("Play or pause (Space)", viewModel.PlayPauseHint);
        Assert.AreEqual("Shell.PlayerBar.PlayPause.Hint", viewModel.PlayPauseHintResourceKey);
        Assert.AreEqual(DesktopDesignTokens.Icon.PlayerPlayPause, viewModel.PlayPauseIconToken);
        Assert.IsFalse(viewModel.CanGoNext);
        Assert.AreEqual("Next track", viewModel.NextIconAccessibilityLabel);
        Assert.AreEqual("Shell.PlayerBar.Next.IconLabel", viewModel.NextIconAccessibilityLabelResourceKey);
        Assert.AreEqual(DesktopDesignTokens.Icon.PlayerNext, viewModel.NextIconToken);
        Assert.AreEqual("Queue", viewModel.QueueIconAccessibilityLabel);
        Assert.AreEqual("Shell.PlayerBar.Queue.IconLabel", viewModel.QueueIconAccessibilityLabelResourceKey);
        Assert.AreEqual("Queue placeholder", viewModel.QueueHint);
        Assert.AreEqual("Shell.PlayerBar.Queue.Hint", viewModel.QueueHintResourceKey);
        Assert.AreEqual(DesktopDesignTokens.Icon.PlayerQueue, viewModel.QueueIconToken);
        Assert.AreEqual("Volume", viewModel.VolumeIconAccessibilityLabel);
        Assert.AreEqual("Shell.PlayerBar.Volume.IconLabel", viewModel.VolumeIconAccessibilityLabelResourceKey);
        Assert.AreEqual("Volume placeholder", viewModel.VolumeHint);
        Assert.AreEqual("Shell.PlayerBar.Volume.Hint", viewModel.VolumeHintResourceKey);
        Assert.AreEqual(DesktopDesignTokens.Icon.PlayerVolume, viewModel.VolumeIconToken);
        Assert.AreEqual("Expanded player", viewModel.ExpandedPlayerIconAccessibilityLabel);
        Assert.AreEqual("Shell.PlayerBar.ExpandedPlayer.IconLabel", viewModel.ExpandedPlayerIconAccessibilityLabelResourceKey);
        Assert.AreEqual("Expanded player placeholder", viewModel.ExpandedPlayerHint);
        Assert.AreEqual("Shell.PlayerBar.ExpandedPlayer.Hint", viewModel.ExpandedPlayerHintResourceKey);
        Assert.AreEqual(DesktopDesignTokens.Icon.PlayerExpanded, viewModel.ExpandedPlayerIconToken);
        Assert.AreEqual(DesktopDesignTokens.Surface.PlayerBar, viewModel.PlayerBarSurfaceToken);
        Assert.AreSame(session.Shell.CommandPalette, viewModel.CommandPalette);
        Assert.AreEqual(session.Shell.CommandPalette.Items.Count, viewModel.CommandPaletteButtons.Count);
        Assert.AreEqual("Go to Home (Ctrl+1)", viewModel.CommandPaletteButtons[0].DisplayLabel);
        Assert.AreSame(session.Shell.StatusBanner, viewModel.StatusBanner);
        Assert.AreEqual("Starting local daemon", viewModel.BackendBannerTitle);
        Assert.AreEqual("Shell.Backend.Starting.Title", viewModel.BackendBannerTitleResourceKey);
        Assert.AreEqual("Sockseek is launching the backend and waiting for a secure session.", viewModel.BackendBannerMessage);
        Assert.AreEqual("Shell.Backend.Starting.Message", viewModel.BackendBannerMessageResourceKey);
        Assert.IsTrue(viewModel.IsBackendBannerVisible);
        Assert.AreEqual(DesktopDesignTokens.Surface.BannerInfo, viewModel.BackendBannerSurfaceToken);
        Assert.AreEqual(DesktopDesignTokens.Icon.BannerInfo, viewModel.BackendBannerIconToken);
        Assert.AreEqual("Backend starting status", viewModel.BackendBannerIconAccessibilityLabel);
        Assert.AreEqual("Shell.Backend.Starting.IconLabel", viewModel.BackendBannerIconAccessibilityLabelResourceKey);
        Assert.AreEqual(ShellSection.Home, viewModel.CurrentSection);
        Assert.AreEqual(ShellSection.Home, viewModel.CurrentPage.Section);
        Assert.AreEqual("Home", viewModel.CurrentPageTitle);
        Assert.AreEqual("Shell.Home.Title", viewModel.CurrentPageTitleResourceKey);
        Assert.AreEqual("Backend status, recent activity, and onboarding live here.", viewModel.CurrentPageDescription);
        Assert.AreEqual("Shell.Home.Description", viewModel.CurrentPageDescriptionResourceKey);
        Assert.AreEqual(DesktopDesignTokens.Icon.Home, viewModel.CurrentPageIconToken);
        Assert.AreEqual("HM", viewModel.CurrentPageBadgeLabel);
        Assert.AreEqual("Daemon, library, and account readiness will anchor this home view.", viewModel.CurrentPageEmptyStateTitle);
        Assert.AreEqual("Shell.Home.EmptyState.Title", viewModel.CurrentPageEmptyStateTitleResourceKey);
        Assert.AreEqual("Use this page to confirm the local backend is healthy, see recent workflows, and pick up any unfinished setup before deeper navigation.", viewModel.CurrentPageEmptyStateDescription);
        Assert.AreEqual("Shell.Home.EmptyState.Description", viewModel.CurrentPageEmptyStateDescriptionResourceKey);
        Assert.AreEqual(3, viewModel.CurrentPageHighlights.Count);
        Assert.AreEqual("Daemon health and handshake", viewModel.CurrentPageHighlights[0].Title);
        Assert.AreEqual("What This Section Will Do", viewModel.PageHighlightsHeading);
        Assert.AreEqual("Shell.Page.Highlights.Title", viewModel.PageHighlightsHeadingResourceKey);
        Assert.AreEqual(BackendConnectionState.Starting, viewModel.BackendState);
        Assert.IsNull(viewModel.CurrentHandshake);
        Assert.IsFalse(viewModel.HasCurrentHandshake);
        Assert.IsFalse(viewModel.IsCommandPaletteOpen);
        Assert.IsFalse(viewModel.CanCopyDiagnostics);
        Assert.IsNull(viewModel.CopyDiagnosticsLabel);
        Assert.IsNull(viewModel.TryGetCopyDiagnosticsText());
        Assert.IsFalse(viewModel.CanStartDaemon);
        Assert.IsFalse(viewModel.IsStartingDaemon);
        Assert.AreEqual("Start local daemon", viewModel.StartDaemonLabel);
        Assert.AreEqual("Shell.Backend.Action.StartDaemon.Label", viewModel.StartDaemonLabelResourceKey);
        Assert.AreEqual("Try starting the local daemon again", viewModel.StartDaemonHint);
        Assert.AreEqual("Shell.Backend.Action.StartDaemon.Hint", viewModel.StartDaemonHintResourceKey);
        StringAssert.Contains(viewModel.DiagnosticsText, "Page: Home");
    }

    [TestMethod]
    public async Task WindowViewModel_ReflectsNavigationThemeAndDiagnosticsUpdatesFromShellSession()
    {
        var supervisor = new DesktopDaemonSupervisor();
        var store = new InMemoryDesktopThemePreferenceStore(DesktopThemePreference.System);
        await using var session = new DesktopShellSession(
            supervisor: supervisor,
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake),
            themePreferenceStore: store);
        var viewModel = new DesktopShellWindowViewModel(session);

        session.Shell.NavigateTo(ShellSection.Downloads);
        session.Shell.SetTheme(DesktopThemePreference.Dark);
        supervisor.TryAcceptHandshakePayload("{\"BaseUrl\":\"http://127.0.0.1:5030\",\"SessionToken\":\"secret-token\"}");
        session.Shell.SetBackendState(BackendConnectionState.Disconnected);

        Assert.AreEqual("Sockseek — Downloads", viewModel.WindowTitle);
        Assert.AreEqual(ShellSection.Downloads, viewModel.CurrentSection);
        Assert.AreEqual(ShellSection.Downloads, viewModel.CurrentPage.Section);
        Assert.AreEqual("Downloads", viewModel.CurrentPageTitle);
        Assert.AreEqual("Shell.Downloads.Title", viewModel.CurrentPageTitleResourceKey);
        Assert.AreEqual("Active and completed download workflows will appear here.", viewModel.CurrentPageDescription);
        Assert.AreEqual("Shell.Downloads.Description", viewModel.CurrentPageDescriptionResourceKey);
        Assert.AreEqual(DesktopDesignTokens.Icon.Downloads, viewModel.CurrentPageIconToken);
        Assert.AreEqual("DL", viewModel.CurrentPageBadgeLabel);
        Assert.AreEqual("No download workflows are running right now.", viewModel.CurrentPageEmptyStateTitle);
        Assert.AreEqual("Shell.Downloads.EmptyState.Title", viewModel.CurrentPageEmptyStateTitleResourceKey);
        Assert.AreEqual("Queued, active, failed, and completed transfers will eventually share one recovery-friendly timeline in this section.", viewModel.CurrentPageEmptyStateDescription);
        Assert.AreEqual("Shell.Downloads.EmptyState.Description", viewModel.CurrentPageEmptyStateDescriptionResourceKey);
        Assert.AreEqual("Queue and transfer health", viewModel.CurrentPageHighlights[0].Title);
        Assert.AreEqual(DesktopThemePreference.Dark, viewModel.CurrentTheme);
        Assert.AreEqual(BackendConnectionState.Disconnected, viewModel.BackendState);
        Assert.AreEqual("Backend disconnected", viewModel.BackendBannerTitle);
        Assert.AreEqual("Shell.Backend.Disconnected.Title", viewModel.BackendBannerTitleResourceKey);
        Assert.AreEqual("Sockseek cannot currently reach the local daemon.", viewModel.BackendBannerMessage);
        Assert.AreEqual("Shell.Backend.Disconnected.Message", viewModel.BackendBannerMessageResourceKey);
        Assert.IsTrue(viewModel.IsBackendBannerVisible);
        Assert.AreEqual(DesktopDesignTokens.Surface.BannerDanger, viewModel.BackendBannerSurfaceToken);
        Assert.AreEqual(DesktopDesignTokens.Icon.BannerDanger, viewModel.BackendBannerIconToken);
        Assert.AreEqual("Backend disconnected status", viewModel.BackendBannerIconAccessibilityLabel);
        Assert.AreEqual("Shell.Backend.Disconnected.IconLabel", viewModel.BackendBannerIconAccessibilityLabelResourceKey);
        Assert.IsNull(viewModel.CurrentHandshake);
        Assert.IsFalse(viewModel.HasCurrentHandshake);
        Assert.IsTrue(viewModel.CanCopyDiagnostics);
        Assert.IsFalse(viewModel.CanStartDaemon);
        Assert.AreEqual("Copy diagnostics", viewModel.CopyDiagnosticsLabel);
        Assert.AreEqual("Shell.Backend.Action.CopyDiagnostics.Label", viewModel.CopyDiagnosticsLabelResourceKey);
        Assert.AreEqual("Copy backend diagnostics", viewModel.CopyDiagnosticsHint);
        Assert.AreEqual("Shell.Backend.Action.CopyDiagnostics.Hint", viewModel.CopyDiagnosticsHintResourceKey);
        var copiedDiagnostics = viewModel.TryGetCopyDiagnosticsText();
        Assert.IsNotNull(copiedDiagnostics);
        StringAssert.Contains(copiedDiagnostics, "Backend state: Disconnected");
        Assert.IsFalse(copiedDiagnostics.Contains("secret-token", StringComparison.Ordinal));
        StringAssert.Contains(viewModel.DiagnosticsText, "Page: Downloads");
        StringAssert.Contains(viewModel.DiagnosticsText, "Theme: Dark");
        StringAssert.Contains(viewModel.DiagnosticsText, "Backend state: Disconnected");
        StringAssert.Contains(viewModel.DiagnosticsText, "Backend URL: unavailable");
        Assert.IsFalse(viewModel.DiagnosticsText.Contains("secret-token", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task WindowViewModel_WhenSessionCanLaunchAndBackendDisconnects_ExposesStartDaemonAction()
    {
        await using var session = new DesktopShellSession(
            supervisor: new DesktopDaemonSupervisor(new FakeProcessLauncher()),
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake),
            workspaceRoot: "/workspace",
            launchRequestFactory: root => new DesktopDaemonLaunchRequest(
                "dotnet",
                "run --project Sockseek.Server/Sockseek.Server.csproj",
                root,
                new Dictionary<string, string?>()));
        var viewModel = new DesktopShellWindowViewModel(session);

        session.Shell.SetBackendState(BackendConnectionState.Disconnected);

        Assert.IsTrue(session.CanStartDaemon);
        Assert.IsTrue(viewModel.CanStartDaemon);
        Assert.AreEqual("Start local daemon", viewModel.StartDaemonLabel);
        Assert.AreEqual("Try starting the local daemon again", viewModel.StartDaemonHint);
    }

    [TestMethod]
    public async Task WindowViewModel_CommandPaletteBridge_ControlsTopLevelShellPaletteState()
    {
        await using var session = new DesktopShellSession(
            supervisor: new DesktopDaemonSupervisor(),
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake));
        var viewModel = new DesktopShellWindowViewModel(session);

        viewModel.OpenCommandPaletteCommand.Execute(null);
        Assert.AreSame(session.Shell.CommandPalette, viewModel.CommandPalette);
        Assert.IsTrue(viewModel.IsCommandPaletteOpen);

        var handledShortcut = viewModel.TryHandleShortcut("Ctrl+K");
        Assert.IsTrue(handledShortcut);
        Assert.IsFalse(viewModel.IsCommandPaletteOpen);

        viewModel.OpenCommandPalette();
        viewModel.CommandPaletteButtons.Single(item => item.Item.Id == "navigate-downloads").ExecuteCommand.Execute(null);

        Assert.AreEqual(ShellSection.Downloads, viewModel.CurrentSection);
        Assert.AreEqual(ShellSection.Downloads, viewModel.CurrentPage.Section);
        Assert.IsFalse(viewModel.IsCommandPaletteOpen);
        Assert.AreEqual("Sockseek — Downloads", viewModel.WindowTitle);
    }

    [TestMethod]
    public async Task WindowViewModel_NavigateAndThemeWrappers_DelegateToShell()
    {
        var store = new InMemoryDesktopThemePreferenceStore(DesktopThemePreference.System);
        await using var session = new DesktopShellSession(
            supervisor: new DesktopDaemonSupervisor(),
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake),
            themePreferenceStore: store);
        var viewModel = new DesktopShellWindowViewModel(session);

        viewModel.NavigationButtons.Single(item => item.Item.Section == ShellSection.Settings).NavigateCommand.Execute(null);
        viewModel.SetDarkThemeCommand.Execute(null);

        Assert.AreEqual(ShellSection.Settings, session.Shell.CurrentSection);
        Assert.AreEqual(ShellSection.Settings, viewModel.CurrentSection);
        Assert.AreEqual("• Settings (Ctrl+,)", viewModel.NavigationButtons.Single(item => item.Item.Section == ShellSection.Settings).DisplayLabel);
        Assert.AreEqual("Home (Ctrl+1)", viewModel.NavigationButtons.Single(item => item.Item.Section == ShellSection.Home).DisplayLabel);
        Assert.AreEqual(DesktopThemePreference.Dark, session.Shell.CurrentTheme);
        Assert.AreEqual(DesktopThemePreference.Dark, viewModel.CurrentTheme);
        Assert.AreEqual(DesktopThemePreference.Dark, store.Load());
    }

    [TestMethod]
    public async Task Dispose_DetachesWindowFromFurtherShellNotifications()
    {
        await using var session = new DesktopShellSession(
            supervisor: new DesktopDaemonSupervisor(),
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake));
        var viewModel = new DesktopShellWindowViewModel(session);
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, eventArgs) => changedProperties.Add(eventArgs.PropertyName);

        viewModel.Dispose();
        viewModel.Dispose();

        session.Shell.NavigateTo(ShellSection.Settings);
        session.Shell.OpenCommandPalette();
        session.Shell.SetTheme(DesktopThemePreference.Dark);

        Assert.AreEqual(0, changedProperties.Count);
    }

    [TestMethod]
    public async Task TryCopyDiagnosticsAsync_WhenActionIsUnavailable_ReturnsFalseWithoutWritingClipboard()
    {
        await using var session = new DesktopShellSession(
            supervisor: new DesktopDaemonSupervisor(),
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake));
        var viewModel = new DesktopShellWindowViewModel(session);
        var clipboard = new FakeDesktopTextClipboard();

        var copied = await viewModel.TryCopyDiagnosticsAsync(clipboard);

        Assert.IsFalse(copied);
        Assert.IsNull(clipboard.CopiedText);
    }

    [TestMethod]
    public async Task TryCopyDiagnosticsAsync_WhenActionIsAvailable_WritesSafeDiagnosticsText()
    {
        var supervisor = new DesktopDaemonSupervisor();
        await using var session = new DesktopShellSession(
            supervisor: supervisor,
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake));
        var viewModel = new DesktopShellWindowViewModel(session);
        var clipboard = new FakeDesktopTextClipboard();

        supervisor.TryAcceptHandshakePayload("{\"BaseUrl\":\"http://127.0.0.1:5030\",\"SessionToken\":\"secret-token\"}");
        session.Shell.SetBackendState(BackendConnectionState.Disconnected);

        var copied = await viewModel.TryCopyDiagnosticsAsync(clipboard);

        Assert.IsTrue(copied);
        Assert.IsNotNull(clipboard.CopiedText);
        StringAssert.Contains(clipboard.CopiedText, "Backend state: Disconnected");
        Assert.IsFalse(clipboard.CopiedText.Contains("secret-token", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task TryStartDaemonAsync_WhenStartIsUnavailable_ReturnsFalse()
    {
        await using var session = new DesktopShellSession(
            supervisor: new DesktopDaemonSupervisor(),
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake));
        var viewModel = new DesktopShellWindowViewModel(session);

        var started = await viewModel.TryStartDaemonAsync();

        Assert.IsFalse(started);
        Assert.AreEqual(BackendConnectionState.Starting, session.Shell.BackendState);
    }

    [TestMethod]
    public async Task TryStartDaemonAsync_WhenAvailable_StartsDaemonThroughSession()
    {
        await using var session = new DesktopShellSession(
            supervisor: new DesktopDaemonSupervisor(new FakeProcessLauncher(
                "SOCKSEEK_DAEMON_HANDSHAKE={\"BaseUrl\":\"http://127.0.0.1:5030\",\"SessionToken\":\"session-token-1\"}")),
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake),
            workspaceRoot: "/workspace",
            launchRequestFactory: root => new DesktopDaemonLaunchRequest(
                "dotnet",
                "run --project Sockseek.Server/Sockseek.Server.csproj",
                root,
                new Dictionary<string, string?>()));
        var viewModel = new DesktopShellWindowViewModel(session);
        session.Shell.SetBackendState(BackendConnectionState.Disconnected);

        var started = await viewModel.TryStartDaemonAsync();
        await session.RecoveryCoordinator.WhenIdleAsync();

        Assert.IsTrue(started);
        Assert.AreEqual(BackendConnectionState.Connected, session.Shell.BackendState);
        Assert.AreEqual(BackendConnectionState.Connected, viewModel.BackendState);
        Assert.IsNotNull(viewModel.CurrentHandshake);
        Assert.IsTrue(viewModel.HasCurrentHandshake);
        Assert.IsFalse(viewModel.IsStartingDaemon);
        Assert.IsFalse(viewModel.CanStartDaemon);
        StringAssert.Contains(viewModel.DiagnosticsText, "Backend state: Connected");
    }

    [TestMethod]
    public async Task TryStartDaemonAsync_WhileLaunching_TogglesBusyStateAndDisablesRepeatStart()
    {
        var processSession = new ControllableProcessSession();
        await using var session = new DesktopShellSession(
            supervisor: new DesktopDaemonSupervisor(new ControlledProcessLauncher(processSession)),
            connectionFactory: handshake => new FakeDesktopEventHubConnection(handshake),
            workspaceRoot: "/workspace",
            launchRequestFactory: root => new DesktopDaemonLaunchRequest(
                "dotnet",
                "run --project Sockseek.Server/Sockseek.Server.csproj",
                root,
                new Dictionary<string, string?>()));
        var viewModel = new DesktopShellWindowViewModel(session);
        session.Shell.SetBackendState(BackendConnectionState.Disconnected);

        var startTask = viewModel.TryStartDaemonAsync();
        await processSession.WaitUntilReadStartedAsync();

        Assert.IsTrue(viewModel.IsStartingDaemon);
        Assert.IsFalse(viewModel.CanStartDaemon);
        Assert.IsFalse(await viewModel.TryStartDaemonAsync());

        processSession.CompleteWith("SOCKSEEK_DAEMON_HANDSHAKE={\"BaseUrl\":\"http://127.0.0.1:5030\",\"SessionToken\":\"session-token-2\"}");
        var started = await startTask;
        await session.RecoveryCoordinator.WhenIdleAsync();

        Assert.IsTrue(started);
        Assert.IsFalse(viewModel.IsStartingDaemon);
        Assert.AreEqual(BackendConnectionState.Connected, session.Shell.BackendState);
    }

    private sealed class FakeProcessLauncher(params string[] outputLines) : IDesktopProcessLauncher
    {
        public Task<IDesktopProcessSession> LaunchAsync(DesktopDaemonLaunchRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<IDesktopProcessSession>(new FakeProcessSession(outputLines));
    }

    private sealed class ControlledProcessLauncher(ControllableProcessSession session) : IDesktopProcessLauncher
    {
        public Task<IDesktopProcessSession> LaunchAsync(DesktopDaemonLaunchRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<IDesktopProcessSession>(session);
    }

    private sealed class FakeProcessSession(params string[] outputLines) : IDesktopProcessSession
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public async IAsyncEnumerable<string> ReadOutputLinesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var line in outputLines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return line;
                await Task.Yield();
            }
        }
    }

    private sealed class ControllableProcessSession : IDesktopProcessSession
    {
        private readonly TaskCompletionSource<bool> readStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<string[]> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task WaitUntilReadStartedAsync() => readStarted.Task;

        public void CompleteWith(params string[] outputLines) => completion.TrySetResult(outputLines);

        public async IAsyncEnumerable<string> ReadOutputLinesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            readStarted.TrySetResult(true);
            var outputLines = await completion.Task.WaitAsync(cancellationToken);
            foreach (var line in outputLines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return line;
                await Task.Yield();
            }
        }
    }

    private sealed class FakeDesktopTextClipboard : IDesktopTextClipboard
    {
        public string? CopiedText { get; private set; }

        public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
        {
            CopiedText = text;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDesktopEventHubConnection(DesktopDaemonHandshake handshake) : IDesktopEventHubConnection
    {
        public DesktopDaemonHandshake Handshake { get; } = handshake;

        public event Func<Exception?, Task>? Reconnecting
        {
            add { }
            remove { }
        }

        public event Func<string?, Task>? Reconnected
        {
            add { }
            remove { }
        }

        public event Func<Exception?, Task>? Closed
        {
            add { }
            remove { }
        }

        public void OnServerEvent(Func<Sockseek.Api.ServerEventEnvelopeDto, Task> handler)
            => _ = handler;

        public void OnWorkflowUpdateBatch(Func<Sockseek.Api.WorkflowUpdateBatchDto, Task> handler)
            => _ = handler;

        public Task StartAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SubscribeAllAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SubscribeWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }
}
