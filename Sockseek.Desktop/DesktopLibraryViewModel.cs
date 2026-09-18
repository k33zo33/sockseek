using System.Windows.Input;
using Sockseek.Api;

namespace Sockseek.Desktop;

public sealed class DesktopLibraryViewModel : ObservableObject
{
    private readonly SockseekApiClient apiClient;
    private IReadOnlyList<LibraryRootDto> roots = [];
    private IReadOnlyList<DesktopLibraryTrackViewModel> tracks = [];
    private IReadOnlyList<DesktopLibraryAlbumGroupViewModel> albumGroups = [];
    private IReadOnlyList<DesktopLibraryDuplicateGroupViewModel> duplicateGroups = [];
    private string rootPath = string.Empty;
    private string rootDisplayName = string.Empty;
    private string searchText = string.Empty;
    private bool includeMissing = true;
    private bool isBusy;
    private string? errorMessage;
    private string? scanSummary;
    private int totalTrackCount;

    public DesktopLibraryViewModel(SockseekApiClient apiClient)
    {
        this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        RefreshRootsCommand = new DesktopAsyncCommand(() => RefreshRootsAsync());
        SaveRootCommand = new DesktopAsyncCommand(() => SaveRootAsync());
        ScanCommand = new DesktopAsyncCommand(() => ScanAsync());
        SearchCommand = new DesktopAsyncCommand(() => SearchAsync());
        RefreshDuplicatesCommand = new DesktopAsyncCommand(() => RefreshDuplicatesAsync());
    }

    public ICommand RefreshRootsCommand { get; }

    public ICommand SaveRootCommand { get; }

    public ICommand ScanCommand { get; }

    public ICommand SearchCommand { get; }

    public ICommand RefreshDuplicatesCommand { get; }

    public IReadOnlyList<LibraryRootDto> Roots
    {
        get => roots;
        private set => SetProperty(ref roots, value);
    }

    public IReadOnlyList<DesktopLibraryTrackViewModel> Tracks
    {
        get => tracks;
        private set
        {
            if (SetProperty(ref tracks, value))
                AlbumGroups = DesktopLibraryAlbumGroups.BuildAlbumGroups(value);
        }
    }

    public IReadOnlyList<DesktopLibraryAlbumGroupViewModel> AlbumGroups
    {
        get => albumGroups;
        private set => SetProperty(ref albumGroups, value);
    }

    public IReadOnlyList<DesktopLibraryDuplicateGroupViewModel> DuplicateGroups
    {
        get => duplicateGroups;
        private set => SetProperty(ref duplicateGroups, value);
    }

    public string RootPath
    {
        get => rootPath;
        set => SetProperty(ref rootPath, value ?? string.Empty);
    }

    public string RootDisplayName
    {
        get => rootDisplayName;
        set => SetProperty(ref rootDisplayName, value ?? string.Empty);
    }

    public string SearchText
    {
        get => searchText;
        set => SetProperty(ref searchText, value ?? string.Empty);
    }

    public bool IncludeMissing
    {
        get => includeMissing;
        set => SetProperty(ref includeMissing, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set => SetProperty(ref isBusy, value);
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    public string? ScanSummary
    {
        get => scanSummary;
        private set => SetProperty(ref scanSummary, value);
    }

    public int TotalTrackCount
    {
        get => totalTrackCount;
        private set => SetProperty(ref totalTrackCount, value);
    }

    public async Task<bool> RefreshRootsAsync(CancellationToken cancellationToken = default)
        => await ExecuteAsync(async () =>
        {
            await LoadRootsAsync(cancellationToken);
            return true;
        });

    public async Task<LibraryRootDto?> SaveRootAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(RootPath))
        {
            ErrorMessage = "Choose a library root path before saving.";
            return null;
        }

        LibraryRootDto? saved = null;
        await ExecuteAsync(async () =>
        {
            saved = await apiClient.SaveLibraryRootAsync(new SaveLibraryRootRequestDto(
                RootPath.Trim(),
                string.IsNullOrWhiteSpace(RootDisplayName) ? null : RootDisplayName.Trim()),
                cancellationToken);
            await LoadRootsAsync(cancellationToken);
            RootPath = string.Empty;
            RootDisplayName = string.Empty;
            return true;
        });

        return saved;
    }

    public async Task<bool> ScanAsync(CancellationToken cancellationToken = default)
        => await ExecuteAsync(async () =>
        {
            var result = await apiClient.ScanLibraryAsync(cancellationToken);
            ScanSummary = $"{result.ScanResult.ImportedFiles} imported, {result.ScanResult.MissingFiles} missing, {result.ScanResult.FailedFiles} failed";
            await LoadTracksAsync(cancellationToken);
            await LoadRootsAsync(cancellationToken);
            return true;
        });

    public async Task<bool> SearchAsync(CancellationToken cancellationToken = default)
        => await ExecuteAsync(async () =>
        {
            await LoadTracksAsync(cancellationToken);
            return true;
        });

    public async Task<bool> RefreshDuplicatesAsync(CancellationToken cancellationToken = default)
        => await ExecuteAsync(async () =>
        {
            var groups = await apiClient.GetLibraryDuplicateGroupsAsync(limit: 100, includeMissing: IncludeMissing, cancellationToken);
            DuplicateGroups = groups.Select(group => new DesktopLibraryDuplicateGroupViewModel(group)).ToArray();
            return true;
        });

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

    private async Task LoadRootsAsync(CancellationToken cancellationToken)
    {
        Roots = await apiClient.GetLibraryRootsAsync(cancellationToken);
    }

    private async Task LoadTracksAsync(CancellationToken cancellationToken)
    {
        var result = await apiClient.SearchLibraryTracksAsync(
            SearchText,
            offset: 0,
            limit: 100,
            includeMissing: IncludeMissing,
            ct: cancellationToken);
        TotalTrackCount = result.TotalCount;
        Tracks = result.Items.Select(track => new DesktopLibraryTrackViewModel(track)).ToArray();
    }
}

public sealed class DesktopLibraryTrackViewModel(LocalLibraryTrackDto track)
{
    public Guid TrackId { get; } = track.TrackId;

    public string Artist { get; } = track.Artist;

    public string Title { get; } = track.Title;

    public string AlbumTitle { get; } = string.IsNullOrWhiteSpace(track.AlbumTitle) ? "Unknown album" : track.AlbumTitle;

    public string AvailabilitySummary { get; } = $"{track.AvailableFileCount} available / {track.MissingFileCount} missing";

    public string BestPath { get; } = track.BestAvailablePath ?? string.Empty;

    public string TechnicalSummary { get; } = BuildTechnicalSummary(track);

    private static string BuildTechnicalSummary(LocalLibraryTrackDto track)
    {
        var parts = new List<string>();
        if (track.DurationMs is { } durationMs)
            parts.Add(TimeSpan.FromMilliseconds(durationMs).ToString(@"m\:ss"));
        if (!string.IsNullOrWhiteSpace(track.Codec))
            parts.Add(track.Codec);
        if (track.Bitrate is { } bitrate)
            parts.Add($"{bitrate} kbps");
        if (track.SampleRate is { } sampleRate)
            parts.Add($"{sampleRate} Hz");
        if (track.BitDepth is { } bitDepth)
            parts.Add($"{bitDepth} bit");
        return string.Join(" | ", parts);
    }
}

public sealed class DesktopLibraryAlbumGroupViewModel(string artist, string albumTitle, IReadOnlyList<DesktopLibraryTrackViewModel> tracks)
{
    public string Artist { get; } = artist;

    public string AlbumTitle { get; } = albumTitle;

    public string DisplayTitle { get; } = $"{artist} - {albumTitle}";

    public string TrackSummary { get; } = $"{tracks.Count} tracks";
}

public sealed class DesktopLibraryDuplicateGroupViewModel(LocalLibraryDuplicateGroupDto group)
{
    public Guid TrackId { get; } = group.TrackId;

    public string DisplayTitle { get; } = $"{group.Artist} - {group.Title}";

    public string FileSummary { get; } = $"{group.FileCount} files";

    public IReadOnlyList<string> Paths { get; } = group.Files.Select(file => file.Path).ToArray();
}

file static class DesktopLibraryAlbumGroups
{
    public static IReadOnlyList<DesktopLibraryAlbumGroupViewModel> BuildAlbumGroups(
        IReadOnlyList<DesktopLibraryTrackViewModel> tracks)
        => tracks
            .GroupBy(track => new { track.Artist, track.AlbumTitle })
            .OrderBy(group => group.Key.Artist, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Key.AlbumTitle, StringComparer.OrdinalIgnoreCase)
            .Select(group => new DesktopLibraryAlbumGroupViewModel(group.Key.Artist, group.Key.AlbumTitle, group.ToList()))
            .ToArray();
}
