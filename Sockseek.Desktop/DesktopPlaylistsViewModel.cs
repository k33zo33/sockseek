using System.Windows.Input;
using Sockseek.Api;

namespace Sockseek.Desktop;

public sealed class DesktopPlaylistsViewModel : ObservableObject
{
    private readonly SockseekApiClient apiClient;
    private IReadOnlyList<DesktopPlaylistSummaryViewModel> playlists = [];
    private DesktopPlaylistDetailViewModel? selectedPlaylist;
    private bool isBusy;
    private string? errorMessage;
    private string? operationSummary;

    public DesktopPlaylistsViewModel(SockseekApiClient apiClient)
    {
        this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        RefreshCommand = new DesktopAsyncCommand(() => RefreshAsync());
        SelectPlaylistCommand = new DesktopAsyncParameterCommand<Guid>(playlistId => SelectPlaylistAsync(playlistId));
        ResolveLocalCommand = new DesktopAsyncCommand(() => ResolveLocalAsync());
        DownloadMissingCommand = new DesktopAsyncCommand(() => DownloadMissingAsync());
        CancelActiveDownloadsCommand = new DesktopAsyncCommand(() => CancelActiveDownloadsAsync());
        PlayAvailableCommand = new DesktopAsyncCommand(() => PlayAvailableAsync());
        PlayItemCommand = new DesktopAsyncParameterCommand<Guid>(playlistItemId => PlayItemAsync(playlistItemId));
        PlayFromHereCommand = new DesktopAsyncParameterCommand<Guid>(playlistItemId => PlayFromHereAsync(playlistItemId));
        SkipItemCommand = new DesktopAsyncParameterCommand<Guid>(playlistItemId => SkipItemAsync(playlistItemId));
        RetryItemCommand = new DesktopAsyncParameterCommand<Guid>(playlistItemId => RetryItemAsync(playlistItemId));
        ApproveLocalMatchCommand = new DesktopAsyncParameterCommand<Guid>(playlistItemId => ApproveLocalMatchAsync(playlistItemId));
        RejectLocalMatchCommand = new DesktopAsyncParameterCommand<Guid>(playlistItemId => RejectLocalMatchAsync(playlistItemId));
    }

    public ICommand RefreshCommand { get; }

    public ICommand SelectPlaylistCommand { get; }

    public ICommand ResolveLocalCommand { get; }

    public ICommand DownloadMissingCommand { get; }

    public ICommand CancelActiveDownloadsCommand { get; }

    public ICommand PlayAvailableCommand { get; }

    public ICommand PlayItemCommand { get; }

    public ICommand PlayFromHereCommand { get; }

    public ICommand SkipItemCommand { get; }

    public ICommand RetryItemCommand { get; }

    public ICommand ApproveLocalMatchCommand { get; }

    public ICommand RejectLocalMatchCommand { get; }

    public IReadOnlyList<DesktopPlaylistSummaryViewModel> Playlists
    {
        get => playlists;
        private set
        {
            if (SetProperty(ref playlists, value))
                OnPropertyChanged(nameof(HasPlaylists));
        }
    }

    public DesktopPlaylistDetailViewModel? SelectedPlaylist
    {
        get => selectedPlaylist;
        private set
        {
            if (!SetProperty(ref selectedPlaylist, value))
                return;

            OnPropertyChanged(nameof(HasSelectedPlaylist));
            OnPropertyChanged(nameof(SelectedPlaylistItems));
            OnPropertyChanged(nameof(CanRunBulkActions));
        }
    }

    public IReadOnlyList<DesktopPlaylistItemViewModel> SelectedPlaylistItems
        => SelectedPlaylist?.Items ?? [];

    public bool HasPlaylists => Playlists.Count > 0;

    public bool HasSelectedPlaylist => SelectedPlaylist is not null;

    public bool CanRunBulkActions => SelectedPlaylist is not null && !IsBusy;

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (!SetProperty(ref isBusy, value))
                return;

            OnPropertyChanged(nameof(CanRunBulkActions));
        }
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    public string? OperationSummary
    {
        get => operationSummary;
        private set => SetProperty(ref operationSummary, value);
    }

    public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
        => await ExecuteAsync(async () =>
        {
            var summaries = await apiClient.GetPlaylistsAsync(cancellationToken);
            Playlists = summaries.Select(summary => new DesktopPlaylistSummaryViewModel(summary)).ToArray();

            var selectedId = SelectedPlaylist?.PlaylistId;
            if (selectedId.HasValue && Playlists.Any(playlist => playlist.PlaylistId == selectedId.Value))
                await LoadPlaylistAsync(selectedId.Value, cancellationToken);
            else if (Playlists.FirstOrDefault()?.PlaylistId is { } firstPlaylistId)
                await LoadPlaylistAsync(firstPlaylistId, cancellationToken);
            else
                SelectedPlaylist = null;

            return true;
        });

    public async Task<bool> SelectPlaylistAsync(Guid playlistId, CancellationToken cancellationToken = default)
        => await ExecuteAsync(async () =>
        {
            var found = await LoadPlaylistAsync(playlistId, cancellationToken);
            if (!found)
            {
                ErrorMessage = "Playlist was not found.";
                return false;
            }

            return true;
        });

    public async Task<bool> ResolveLocalAsync(CancellationToken cancellationToken = default)
        => await ExecuteSelectedPlaylistAsync(async playlistId =>
        {
            var result = await apiClient.ResolvePlaylistLocalAsync(playlistId, cancellationToken);
            if (result is null)
                return MissingSelectedPlaylist();

            ApplyPlaylist(result.Playlist);
            OperationSummary = $"{result.MatchedItems} matched, {result.ReviewItems} review, {result.UnresolvedItems} unresolved";
            return true;
        });

    public async Task<bool> DownloadMissingAsync(CancellationToken cancellationToken = default)
        => await ExecuteSelectedPlaylistAsync(async playlistId =>
        {
            var result = await apiClient.DownloadMissingPlaylistItemsAsync(playlistId, cancellationToken);
            if (result is null)
                return MissingSelectedPlaylist();

            ApplyPlaylist(result.Playlist);
            OperationSummary = $"{result.SubmittedItems} submitted, {result.FailedItems} failed, {result.SkippedItems} skipped";
            return true;
        });

    public async Task<bool> CancelActiveDownloadsAsync(CancellationToken cancellationToken = default)
        => await ExecuteSelectedPlaylistAsync(async playlistId =>
        {
            var result = await apiClient.CancelPlaylistDownloadsAsync(playlistId, cancellationToken);
            if (result is null)
                return MissingSelectedPlaylist();

            ApplyPlaylist(result.Playlist);
            OperationSummary = $"{result.CancelledItems} cancelled, {result.FailedItems} failed";
            return true;
        });

    public async Task<bool> PlayAvailableAsync(CancellationToken cancellationToken = default)
        => await ExecuteSelectedPlaylistAsync(async playlistId =>
        {
            var state = await apiClient.PlayAvailablePlaylistItemsAsync(playlistId, cancellationToken);
            if (state is null)
                return MissingSelectedPlaylist();

            OperationSummary = $"{state.Queue.Items.Count} available queued";
            return true;
        });

    public async Task<bool> PlayItemAsync(Guid playlistItemId, CancellationToken cancellationToken = default)
        => await ExecuteSelectedPlaylistAsync(async playlistId =>
        {
            var item = FindSelectedItem(playlistItemId);
            if (item == null)
            {
                ErrorMessage = "Playlist item was not found.";
                return false;
            }

            if (item.CanPlayLocal)
            {
                await apiClient.PlayPlaylistItemAsync(playlistItemId, cancellationToken);
                OperationSummary = "Playback started";
                return true;
            }

            if (item.IsResolutionActive)
            {
                OperationSummary = "Item is already resolving";
                return true;
            }

            if (!item.CanStartResolution)
            {
                ErrorMessage = "Resolve or review this item before playback.";
                return false;
            }

            var result = await apiClient.DownloadPlaylistItemAsync(playlistId, playlistItemId, cancellationToken);
            if (result is null)
            {
                ErrorMessage = "Playlist item was not found.";
                return false;
            }

            ApplyPlaylist(result.Playlist);
            OperationSummary = $"{result.SubmittedItems} download submitted for playback, {result.FailedItems} failed";
            return true;
        });

    public async Task<bool> PlayFromHereAsync(Guid playlistItemId, CancellationToken cancellationToken = default)
        => await ExecuteSelectedPlaylistAsync(async playlistId =>
        {
            var state = await apiClient.PlayPlaylistFromItemAsync(playlistId, playlistItemId, cancellationToken);
            if (state is null)
            {
                ErrorMessage = "Playlist item was not found.";
                return false;
            }

            OperationSummary = $"{state.Queue.Items.Count} available queued from here";
            return true;
        });

    private DesktopPlaylistItemViewModel? FindSelectedItem(Guid playlistItemId)
        => SelectedPlaylistItems.SingleOrDefault(item => item.PlaylistItemId == playlistItemId);

    public async Task<bool> SkipItemAsync(Guid playlistItemId, CancellationToken cancellationToken = default)
        => await ExecuteSelectedPlaylistAsync(async playlistId =>
        {
            var playlist = await apiClient.SkipPlaylistItemAsync(playlistId, playlistItemId, cancellationToken);
            if (playlist is null)
            {
                ErrorMessage = "Playlist item was not found.";
                return false;
            }

            ApplyPlaylist(playlist);
            OperationSummary = "Item skipped";
            return true;
        });

    public async Task<bool> RetryItemAsync(Guid playlistItemId, CancellationToken cancellationToken = default)
        => await ExecuteSelectedPlaylistAsync(async playlistId =>
        {
            var result = await apiClient.RetryPlaylistItemAsync(playlistId, playlistItemId, cancellationToken);
            if (result is null)
            {
                ErrorMessage = "Playlist item was not found.";
                return false;
            }

            ApplyPlaylist(result.Playlist);
            OperationSummary = $"{result.SubmittedItems} retry submitted, {result.FailedItems} failed";
            return true;
        });

    public async Task<bool> ApproveLocalMatchAsync(Guid playlistItemId, CancellationToken cancellationToken = default)
        => await ReviewLocalMatchAsync(
            playlistItemId,
            (playlistId, itemId) => apiClient.ApprovePlaylistItemLocalMatchAsync(playlistId, itemId, cancellationToken),
            "Local match approved");

    public async Task<bool> RejectLocalMatchAsync(Guid playlistItemId, CancellationToken cancellationToken = default)
        => await ReviewLocalMatchAsync(
            playlistItemId,
            (playlistId, itemId) => apiClient.RejectPlaylistItemLocalMatchAsync(playlistId, itemId, cancellationToken),
            "Local match rejected");

    private async Task<bool> ReviewLocalMatchAsync(
        Guid playlistItemId,
        Func<Guid, Guid, Task<PlaylistDetailDto?>> reviewAction,
        string summary)
        => await ExecuteSelectedPlaylistAsync(async playlistId =>
        {
            var playlist = await reviewAction(playlistId, playlistItemId);
            if (playlist is null)
            {
                ErrorMessage = "Playlist item was not found.";
                return false;
            }

            ApplyPlaylist(playlist);
            OperationSummary = summary;
            return true;
        });

    private async Task<bool> ExecuteSelectedPlaylistAsync(Func<Guid, Task<bool>> action)
    {
        if (SelectedPlaylist is null)
        {
            ErrorMessage = "Select a playlist first.";
            return false;
        }

        return await ExecuteAsync(() => action(SelectedPlaylist.PlaylistId));
    }

    private async Task<bool> ExecuteAsync(Func<Task<bool>> action)
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            return await action();
        }
        catch (SockseekApiRequestException exception)
        {
            ErrorMessage = exception.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> LoadPlaylistAsync(Guid playlistId, CancellationToken cancellationToken)
    {
        var detail = await apiClient.GetPlaylistAsync(playlistId, cancellationToken);
        if (detail is null)
            return false;

        ApplyPlaylist(detail);
        return true;
    }

    private void ApplyPlaylist(PlaylistDetailDto playlist)
    {
        SelectedPlaylist = new DesktopPlaylistDetailViewModel(playlist);
        Playlists = Playlists
            .Select(summary => summary.PlaylistId == playlist.PlaylistId
                ? DesktopPlaylistSummaryViewModel.FromDetail(playlist)
                : summary)
            .ToArray();
    }

    private bool MissingSelectedPlaylist()
    {
        ErrorMessage = "Playlist was not found.";
        SelectedPlaylist = null;
        return false;
    }
}

public sealed class DesktopPlaylistSummaryViewModel
{
    public DesktopPlaylistSummaryViewModel(PlaylistSummaryDto playlist)
    {
        PlaylistId = playlist.PlaylistId;
        Name = playlist.Name;
        ProviderLabel = string.IsNullOrWhiteSpace(playlist.ProviderId) ? "Local" : playlist.ProviderId;
        ImportMode = playlist.ImportMode;
        ResolutionSummary = FormatResolution(playlist.Resolution);
        UpdatedSummary = $"Updated {playlist.UpdatedAtUtc:yyyy-MM-dd HH:mm} UTC";
    }

    private DesktopPlaylistSummaryViewModel(PlaylistDetailDto playlist)
    {
        PlaylistId = playlist.PlaylistId;
        Name = playlist.Name;
        ProviderLabel = string.IsNullOrWhiteSpace(playlist.ProviderId) ? "Local" : playlist.ProviderId;
        ImportMode = playlist.ImportMode;
        ResolutionSummary = FormatResolution(playlist.Resolution);
        UpdatedSummary = $"Updated {playlist.UpdatedAtUtc:yyyy-MM-dd HH:mm} UTC";
    }

    public Guid PlaylistId { get; }

    public string Name { get; }

    public string ProviderLabel { get; }

    public string ImportMode { get; }

    public string ResolutionSummary { get; }

    public string UpdatedSummary { get; }

    public static DesktopPlaylistSummaryViewModel FromDetail(PlaylistDetailDto playlist)
        => new(playlist);

    private static string FormatResolution(PlaylistResolutionSummaryDto resolution)
        => $"{resolution.AvailableLocalItems}/{resolution.TotalItems} available, {resolution.UnresolvedItems} missing, {resolution.FailedItems} failed";
}

public sealed class DesktopPlaylistDetailViewModel
{
    public DesktopPlaylistDetailViewModel(PlaylistDetailDto playlist)
    {
        PlaylistId = playlist.PlaylistId;
        Name = playlist.Name;
        ProviderLabel = string.IsNullOrWhiteSpace(playlist.ProviderId) ? "Local" : playlist.ProviderId;
        ImportMode = playlist.ImportMode;
        SourceSummary = string.IsNullOrWhiteSpace(playlist.ExternalUrl)
            ? $"{ProviderLabel} playlist"
            : playlist.ExternalUrl;
        ResolutionSummary = DesktopPlaylistSummaryViewModel.FromDetail(playlist).ResolutionSummary;
        ActivitySummary = $"{playlist.Resolution.SearchingItems} searching, {playlist.Resolution.DownloadingItems} downloading, {playlist.Resolution.ReviewRequiredItems + playlist.Resolution.CandidateFoundItems} review";
        Items = playlist.Items.Select(item => new DesktopPlaylistItemViewModel(item)).ToArray();
    }

    public Guid PlaylistId { get; }

    public string Name { get; }

    public string ProviderLabel { get; }

    public string ImportMode { get; }

    public string SourceSummary { get; }

    public string ResolutionSummary { get; }

    public string ActivitySummary { get; }

    public IReadOnlyList<DesktopPlaylistItemViewModel> Items { get; }
}

public sealed class DesktopPlaylistItemViewModel(PlaylistItemDto item)
{
    public Guid PlaylistItemId { get; } = item.PlaylistItemId;

    public string PositionLabel { get; } = item.Position.ToString();

    public string Title { get; } = string.IsNullOrWhiteSpace(item.Title) ? "Untitled" : item.Title;

    public string ArtistSummary { get; } = item.Artists.Count == 0 ? "Unknown artist" : string.Join(", ", item.Artists);

    public string AlbumTitle { get; } = string.IsNullOrWhiteSpace(item.Album) ? "Unknown album" : item.Album;

    public string DurationSummary { get; } = item.DurationMs is { } durationMs
        ? TimeSpan.FromMilliseconds(durationMs).ToString(@"m\:ss")
        : string.Empty;

    public string Status { get; } = item.Status;

    public string SourceSummary { get; } = string.IsNullOrWhiteSpace(item.ExternalUrl) ? item.ProviderItemId : item.ExternalUrl;

    public bool CanPlayLocal { get; } = item.CanonicalTrackId.HasValue && string.Equals(item.Status, "AvailableLocal", StringComparison.Ordinal);

    public bool CanStartResolution { get; } = string.Equals(item.Status, "Imported", StringComparison.Ordinal)
        || string.Equals(item.Status, "Unresolved", StringComparison.Ordinal)
        || string.Equals(item.Status, "Failed", StringComparison.Ordinal)
        || string.Equals(item.Status, "Skipped", StringComparison.Ordinal);

    public bool IsResolutionActive { get; } = string.Equals(item.Status, "Searching", StringComparison.Ordinal)
        || string.Equals(item.Status, "CandidateFound", StringComparison.Ordinal)
        || string.Equals(item.Status, "Downloading", StringComparison.Ordinal);

    public bool CanPlay { get; } = !string.Equals(item.Status, "RemovedFromSourcePlaylist", StringComparison.Ordinal);

    public bool CanPlayFromHere { get; } = !string.Equals(item.Status, "RemovedFromSourcePlaylist", StringComparison.Ordinal);

    public bool CanSkip { get; } = !string.Equals(item.Status, "RemovedFromSourcePlaylist", StringComparison.Ordinal);

    public bool CanRetry { get; } = string.Equals(item.Status, "Failed", StringComparison.Ordinal)
        || string.Equals(item.Status, "Skipped", StringComparison.Ordinal);

    public bool CanReviewLocal { get; } = string.Equals(item.Status, "ReviewRequired", StringComparison.Ordinal);
}
