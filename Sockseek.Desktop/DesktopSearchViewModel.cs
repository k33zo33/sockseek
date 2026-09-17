using Sockseek.Api;

namespace Sockseek.Desktop;

public sealed class DesktopSearchViewModel : ObservableObject
{
    private readonly SockseekApiClient apiClient;
    private DesktopSearchMode mode = DesktopSearchMode.Track;
    private string artist = string.Empty;
    private string title = string.Empty;
    private string album = string.Empty;
    private string searchHint = string.Empty;
    private bool isBusy;
    private JobSummaryDto? lastJob;
    private string? errorMessage;

    public DesktopSearchViewModel(SockseekApiClient apiClient)
        => this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));

    public DesktopSearchMode Mode
    {
        get => mode;
        set => SetProperty(ref mode, value);
    }

    public string Artist
    {
        get => artist;
        set => SetProperty(ref artist, value ?? string.Empty);
    }

    public string Title
    {
        get => title;
        set => SetProperty(ref title, value ?? string.Empty);
    }

    public string Album
    {
        get => album;
        set => SetProperty(ref album, value ?? string.Empty);
    }

    public string SearchHint
    {
        get => searchHint;
        set => SetProperty(ref searchHint, value ?? string.Empty);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set => SetProperty(ref isBusy, value);
    }

    public JobSummaryDto? LastJob
    {
        get => lastJob;
        private set => SetProperty(ref lastJob, value);
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    public async Task<JobSummaryDto?> SearchAsync(CancellationToken cancellationToken = default)
    {
        ErrorMessage = null;
        if (!HasQuery())
        {
            ErrorMessage = Mode == DesktopSearchMode.Track
                ? "Enter an artist or title before searching."
                : "Enter an artist or album before searching.";
            return null;
        }

        IsBusy = true;
        try
        {
            LastJob = Mode == DesktopSearchMode.Track
                ? await apiClient.SubmitTrackSearchJobAsync(
                    new SubmitTrackSearchJobRequestDto(
                        new SongQueryDto(
                            EmptyToNull(Artist),
                            EmptyToNull(Title),
                            EmptyToNull(Album))),
                    cancellationToken)
                : await apiClient.SubmitAlbumSearchJobAsync(
                    new SubmitAlbumSearchJobRequestDto(
                        new AlbumQueryDto(
                            EmptyToNull(Artist),
                            EmptyToNull(Album),
                            EmptyToNull(SearchHint))),
                    cancellationToken);

            return LastJob;
        }
        catch (SockseekApiRequestException exception)
        {
            ErrorMessage = exception.Message;
            LastJob = null;
            return null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool HasQuery()
        => Mode == DesktopSearchMode.Track
            ? !string.IsNullOrWhiteSpace(Artist) || !string.IsNullOrWhiteSpace(Title)
            : !string.IsNullOrWhiteSpace(Artist) || !string.IsNullOrWhiteSpace(Album);

    private static string? EmptyToNull(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}