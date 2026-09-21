using Sockseek.Api;

namespace Sockseek.Desktop;

public sealed class PlayerBarPlaceholderViewModel : ObservableObject
{
    private SockseekApiClient? apiClient;
    private PlayerStateDto? state;
    private string? errorMessage;
    private bool isBusy;
    private bool isQueueExpanded;
    private int connectionRevision;

    public PlayerBarPlaceholderViewModel()
    {
        TransportActions =
        [
            new(
                "\u23EE",
                DesktopDesignTokens.Icon.PlayerPrevious,
                DesktopStringResources.Get("Shell.PlayerBar.Previous.IconLabel"),
                "Shell.PlayerBar.Previous.IconLabel",
                DesktopStringResources.Get("Shell.PlayerBar.Previous.IconLabel"),
                "Shell.PlayerBar.Previous.IconLabel",
                false,
                () => _ = PreviousAsync()),
            new(
                "\u23EF",
                DesktopDesignTokens.Icon.PlayerPlayPause,
                DesktopStringResources.Get("Shell.PlayerBar.PlayPause.IconLabel"),
                "Shell.PlayerBar.PlayPause.IconLabel",
                DesktopStringResources.Get("Shell.PlayerBar.PlayPause.Hint"),
                "Shell.PlayerBar.PlayPause.Hint",
                false,
                () => _ = TogglePlayPauseAsync()),
            new(
                "\u23ED",
                DesktopDesignTokens.Icon.PlayerNext,
                DesktopStringResources.Get("Shell.PlayerBar.Next.IconLabel"),
                "Shell.PlayerBar.Next.IconLabel",
                DesktopStringResources.Get("Shell.PlayerBar.Next.IconLabel"),
                "Shell.PlayerBar.Next.IconLabel",
                false,
                () => _ = NextAsync()),
        ];

        UtilityActions =
        [
            new(
                "\u2261",
                DesktopDesignTokens.Icon.PlayerQueue,
                DesktopStringResources.Get("Shell.PlayerBar.Queue.IconLabel"),
                "Shell.PlayerBar.Queue.IconLabel",
                DesktopStringResources.Get("Shell.PlayerBar.Queue.Hint"),
                "Shell.PlayerBar.Queue.Hint",
                false,
                ToggleQueueExpanded),
            new(
                "\U0001F50A",
                DesktopDesignTokens.Icon.PlayerVolume,
                DesktopStringResources.Get("Shell.PlayerBar.Volume.IconLabel"),
                "Shell.PlayerBar.Volume.IconLabel",
                DesktopStringResources.Get("Shell.PlayerBar.Volume.Hint"),
                "Shell.PlayerBar.Volume.Hint",
                false,
                () => _ = ToggleMuteAsync()),
            new(
                "\u21F1",
                DesktopDesignTokens.Icon.PlayerExpanded,
                DesktopStringResources.Get("Shell.PlayerBar.ExpandedPlayer.IconLabel"),
                "Shell.PlayerBar.ExpandedPlayer.IconLabel",
                DesktopStringResources.Get("Shell.PlayerBar.ExpandedPlayer.Hint"),
                "Shell.PlayerBar.ExpandedPlayer.Hint",
                false,
                ToggleQueueExpanded),
        ];
    }

    public string TitleResourceKey { get; } = "Shell.PlayerBar.Title";

    public string Title
        => state?.Path is { Length: > 0 } path
            ? Path.GetFileName(path)
            : DesktopStringResources.Get("Shell.PlayerBar.Title");

    public string ArtworkResourceKey { get; } = "Shell.PlayerBar.Artwork";

    public string Artwork { get; } = DesktopStringResources.Get("Shell.PlayerBar.Artwork");

    public string ArtworkIconAccessibilityLabelResourceKey { get; } = "Shell.PlayerBar.Artwork.IconLabel";

    public string ArtworkIconAccessibilityLabel { get; } = DesktopStringResources.Get("Shell.PlayerBar.Artwork.IconLabel");

    public string ArtistResourceKey { get; } = "Shell.PlayerBar.Artist";

    public string Artist
        => errorMessage
            ?? state?.ErrorMessage
            ?? (apiClient is null
                ? DesktopStringResources.Get("Shell.PlayerBar.Artist")
                : "Local player ready");

    public string ProgressResourceKey { get; } = "Shell.PlayerBar.Progress";

    public string Progress
        => state is null
            ? DesktopStringResources.Get("Shell.PlayerBar.Progress")
            : $"{FormatTime(TimeSpan.FromMilliseconds(state.PositionMs))} / --:--";

    public string ProgressHintResourceKey { get; } = "Shell.PlayerBar.Progress.Hint";

    public string ProgressHint { get; } = DesktopStringResources.Get("Shell.PlayerBar.Progress.Hint");

    public bool CanGoPrevious => apiClient is not null && !isBusy && (state?.Queue.Items.Count ?? 0) > 0;

    public string PreviousIconAccessibilityLabelResourceKey { get; } = "Shell.PlayerBar.Previous.IconLabel";

    public string PreviousIconAccessibilityLabel { get; } = DesktopStringResources.Get("Shell.PlayerBar.Previous.IconLabel");

    public string PreviousIconToken { get; } = DesktopDesignTokens.Icon.PlayerPrevious;

    public bool CanPlayPause => apiClient is not null && !isBusy && state?.State is "Playing" or "Paused";

    public string PlayPauseIconAccessibilityLabelResourceKey { get; } = "Shell.PlayerBar.PlayPause.IconLabel";

    public string PlayPauseIconAccessibilityLabel { get; } = DesktopStringResources.Get("Shell.PlayerBar.PlayPause.IconLabel");

    public string PlayPauseHintResourceKey { get; } = "Shell.PlayerBar.PlayPause.Hint";

    public string PlayPauseHint { get; } = DesktopStringResources.Get("Shell.PlayerBar.PlayPause.Hint");

    public string PlayPauseIconToken { get; } = DesktopDesignTokens.Icon.PlayerPlayPause;

    public bool CanGoNext => apiClient is not null && !isBusy && (state?.Queue.Items.Count ?? 0) > 0;

    public string NextIconAccessibilityLabelResourceKey { get; } = "Shell.PlayerBar.Next.IconLabel";

    public string NextIconAccessibilityLabel { get; } = DesktopStringResources.Get("Shell.PlayerBar.Next.IconLabel");

    public string NextIconToken { get; } = DesktopDesignTokens.Icon.PlayerNext;

    public string QueueSummaryResourceKey { get; } = "Shell.PlayerBar.QueueSummary";

    public string QueueSummary
    {
        get
        {
            if (apiClient is null || state is null)
                return DesktopStringResources.Get("Shell.PlayerBar.QueueSummary");

            var count = state.Queue.Items.Count;
            return count == 0
                ? "Queue empty"
                : $"{count} queued - {state.Queue.RepeatMode}";
        }
    }

    public string QueueIconAccessibilityLabelResourceKey { get; } = "Shell.PlayerBar.Queue.IconLabel";

    public string QueueIconAccessibilityLabel { get; } = DesktopStringResources.Get("Shell.PlayerBar.Queue.IconLabel");

    public string QueueHintResourceKey { get; } = "Shell.PlayerBar.Queue.Hint";

    public string QueueHint { get; } = DesktopStringResources.Get("Shell.PlayerBar.Queue.Hint");

    public string VolumeIconAccessibilityLabelResourceKey { get; } = "Shell.PlayerBar.Volume.IconLabel";

    public string VolumeIconAccessibilityLabel { get; } = DesktopStringResources.Get("Shell.PlayerBar.Volume.IconLabel");

    public string VolumeHintResourceKey { get; } = "Shell.PlayerBar.Volume.Hint";

    public string VolumeHint
        => state is null
            ? DesktopStringResources.Get("Shell.PlayerBar.Volume.Hint")
            : $"{(state.IsMuted ? "Muted" : "Volume")} {(int)Math.Round(state.Volume * 100)}%";

    public string ExpandedPlayerIconAccessibilityLabelResourceKey { get; } = "Shell.PlayerBar.ExpandedPlayer.IconLabel";

    public string ExpandedPlayerIconAccessibilityLabel { get; } = DesktopStringResources.Get("Shell.PlayerBar.ExpandedPlayer.IconLabel");

    public string ExpandedPlayerHintResourceKey { get; } = "Shell.PlayerBar.ExpandedPlayer.Hint";

    public string ExpandedPlayerHint { get; } = DesktopStringResources.Get("Shell.PlayerBar.ExpandedPlayer.Hint");

    public string SurfaceToken { get; } = DesktopDesignTokens.Surface.PlayerBar;

    public string TitleTypographyToken { get; } = DesktopDesignTokens.Typography.PlayerTitle;

    public string ArtistTypographyToken { get; } = DesktopDesignTokens.Typography.PlayerSubtitle;

    public string QueueIconToken { get; } = DesktopDesignTokens.Icon.PlayerQueue;

    public string VolumeIconToken { get; } = DesktopDesignTokens.Icon.PlayerVolume;

    public string ExpandedPlayerIconToken { get; } = DesktopDesignTokens.Icon.PlayerExpanded;

    public string PaddingToken { get; } = DesktopDesignTokens.Spacing.PlayerBarPadding;

    public IReadOnlyList<DesktopPlayerBarActionViewModel> TransportActions { get; }

    public IReadOnlyList<DesktopPlayerBarActionViewModel> UtilityActions { get; }

    public bool IsQueueExpanded => isQueueExpanded;

    public string ExpandedQueueTitle
        => state is null
            ? "Player queue"
            : $"Player queue ({state.Queue.Items.Count})";

    public string ExpandedQueueEmptyMessage
        => apiClient is null
            ? DesktopStringResources.Get("Shell.PlayerBar.QueueSummary")
            : "Queue empty";

    public IReadOnlyList<DesktopPlayerQueueItemViewModel> QueueItems
        => state?.Queue.Items
            .Select((item, index) => CreateQueueItemViewModel(state, item, index))
            .ToArray()
            ?? [];

    public bool HasQueueItems => QueueItems.Count > 0;

    public bool IsQueueEmpty => !HasQueueItems;

    public async Task ConnectAsync(SockseekApiClient client, CancellationToken cancellationToken = default)
    {
        apiClient = client ?? throw new ArgumentNullException(nameof(client));
        connectionRevision++;
        await RefreshAsync(cancellationToken);
    }

    public void Disconnect()
    {
        apiClient = null;
        state = null;
        errorMessage = null;
        isBusy = false;
        isQueueExpanded = false;
        connectionRevision++;
        NotifyStateChanged();
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (apiClient is null)
            return;

        await RunCommandAsync(client => client.GetPlayerStateAsync(cancellationToken));
    }

    internal bool TryHandleInput(DesktopPlayerInput input)
    {
        var action = input switch
        {
            DesktopPlayerInput.Previous => TransportActions[0],
            DesktopPlayerInput.TogglePlayPause => TransportActions[1],
            DesktopPlayerInput.Next => TransportActions[2],
            DesktopPlayerInput.ToggleMute => UtilityActions[1],
            _ => null,
        };

        if (action is null || !action.IsEnabled)
            return false;

            action.Command.Execute(null);
        return true;
    }

    private void ToggleQueueExpanded()
    {
        if (apiClient is null)
            return;

        isQueueExpanded = !isQueueExpanded;
        OnPropertyChanged(nameof(IsQueueExpanded));
    }

    private async Task TogglePlayPauseAsync()
    {
        if (apiClient is null || state is null)
            return;

        if (state.State == "Playing")
            await RunCommandAsync(client => client.PausePlaybackAsync());
        else if (state.State == "Paused")
            await RunCommandAsync(client => client.ResumePlaybackAsync());
    }

    private async Task PreviousAsync()
    {
        if (apiClient is null)
            return;

        await RunCommandAsync(client => client.PreviousPlaybackItemAsync());
    }

    private async Task NextAsync()
    {
        if (apiClient is null)
            return;

        await RunCommandAsync(client => client.NextPlaybackItemAsync());
    }

    private async Task ToggleMuteAsync()
    {
        if (apiClient is null)
            return;

        await RunCommandAsync(client => client.SetPlayerMutedAsync(!(state?.IsMuted ?? false)));
    }

    private async Task RunCommandAsync(Func<SockseekApiClient, Task<PlayerStateDto>> command)
    {
        var commandClient = apiClient;
        if (commandClient is null)
            return;

        var commandRevision = connectionRevision;
        isBusy = true;
        NotifyControlStateChanged();
        try
        {
            var nextState = await command(commandClient);
            if (!IsCurrentConnection(commandClient, commandRevision))
                return;

            state = nextState;
            errorMessage = null;
        }
        catch (OperationCanceledException) when (!IsCurrentConnection(commandClient, commandRevision))
        {
        }
        catch (Exception ex)
        {
            if (!IsCurrentConnection(commandClient, commandRevision))
                return;

            errorMessage = ex.Message;
        }
        finally
        {
            if (IsCurrentConnection(commandClient, commandRevision))
            {
                isBusy = false;
                NotifyStateChanged();
            }
        }
    }

    private bool IsCurrentConnection(SockseekApiClient client, int revision)
        => ReferenceEquals(apiClient, client) && connectionRevision == revision;

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Artist));
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(QueueSummary));
        OnPropertyChanged(nameof(VolumeHint));
        OnPropertyChanged(nameof(ExpandedQueueTitle));
        OnPropertyChanged(nameof(ExpandedQueueEmptyMessage));
        OnPropertyChanged(nameof(QueueItems));
        OnPropertyChanged(nameof(HasQueueItems));
        OnPropertyChanged(nameof(IsQueueEmpty));
        NotifyControlStateChanged();
    }

    private void NotifyControlStateChanged()
    {
        TransportActions[0].IsEnabled = CanGoPrevious;
        TransportActions[1].IsEnabled = CanPlayPause;
        TransportActions[2].IsEnabled = CanGoNext;
        UtilityActions[0].IsEnabled = apiClient is not null && !isBusy;
        UtilityActions[1].IsEnabled = apiClient is not null && !isBusy;
        UtilityActions[2].IsEnabled = apiClient is not null && !isBusy;
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanPlayPause));
        OnPropertyChanged(nameof(CanGoNext));
    }

    private static DesktopPlayerQueueItemViewModel CreateQueueItemViewModel(
        PlayerStateDto state,
        PlayerQueueItemDto item,
        int index)
    {
        var isCurrent = index == state.Queue.CurrentIndex;
        var title = isCurrent && !string.IsNullOrWhiteSpace(state.Path)
            ? Path.GetFileName(state.Path)
            : $"Track {ShortId(item.CanonicalTrackId)}";
        var detail = item.LocalMediaFileId is Guid localMediaFileId
            ? $"Canonical {ShortId(item.CanonicalTrackId)} - Local {ShortId(localMediaFileId)}"
            : $"Canonical {ShortId(item.CanonicalTrackId)}";

        return new DesktopPlayerQueueItemViewModel(
            (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            title,
            detail,
            isCurrent);
    }

    private static string ShortId(Guid id) => id.ToString("N")[..8];

    private static string FormatTime(TimeSpan value)
        => value.TotalHours >= 1
            ? value.ToString(@"h\:mm\:ss")
            : value.ToString(@"mm\:ss");
}
