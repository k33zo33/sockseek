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
    private IReadOnlyList<FileCandidateDto> fileCandidates = [];
    private IReadOnlyList<AlbumFolderDto> folderCandidates = [];
    private IReadOnlyList<JobSummaryDto> lastDownloadJobs = [];
    private int resultsRevision;
    private bool isResultsComplete;

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

    public IReadOnlyList<FileCandidateDto> FileCandidates
    {
        get => fileCandidates;
        private set => SetProperty(ref fileCandidates, value);
    }

    public IReadOnlyList<AlbumFolderDto> FolderCandidates
    {
        get => folderCandidates;
        private set => SetProperty(ref folderCandidates, value);
    }

    public int ResultsRevision
    {
        get => resultsRevision;
        private set => SetProperty(ref resultsRevision, value);
    }

    public bool IsResultsComplete
    {
        get => isResultsComplete;
        private set => SetProperty(ref isResultsComplete, value);
    }

    public IReadOnlyList<JobSummaryDto> LastDownloadJobs
    {
        get => lastDownloadJobs;
        private set => SetProperty(ref lastDownloadJobs, value);
    }

    public async Task<JobSummaryDto?> SearchAsync(CancellationToken cancellationToken = default)
    {
        ErrorMessage = null;
        ClearResults();
        LastDownloadJobs = [];
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

    public async Task<bool> RefreshResultsAsync(CancellationToken cancellationToken = default)
    {
        ErrorMessage = null;
        if (LastJob is null)
        {
            ErrorMessage = "Start a search before loading results.";
            return false;
        }

        IsBusy = true;
        try
        {
            if (Mode == DesktopSearchMode.Track)
            {
                var snapshot = await apiClient.GetFileResultsAsync(LastJob.JobId, cancellationToken);
                if (snapshot is null)
                {
                    ErrorMessage = "Search results are not available yet.";
                    return false;
                }

                FileCandidates = snapshot.Items;
                FolderCandidates = [];
                ResultsRevision = snapshot.Revision;
                IsResultsComplete = snapshot.IsComplete;
            }
            else
            {
                var snapshot = await apiClient.GetFolderResultsAsync(LastJob.JobId, includeFiles: false, cancellationToken);
                if (snapshot is null)
                {
                    ErrorMessage = "Search results are not available yet.";
                    return false;
                }

                FolderCandidates = snapshot.Items;
                FileCandidates = [];
                ResultsRevision = snapshot.Revision;
                IsResultsComplete = snapshot.IsComplete;
            }

            return true;
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

    public async Task<bool> DownloadFileAsync(FileCandidateDto candidate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ErrorMessage = null;
        if (LastJob is null)
        {
            ErrorMessage = "Start a search before downloading a candidate.";
            return false;
        }

        IsBusy = true;
        try
        {
            var jobs = await apiClient.StartFileDownloadsAsync(
                LastJob.JobId,
                new StartFileDownloadsRequestDto([candidate.Ref]),
                cancellationToken);
            if (jobs is null)
            {
                ErrorMessage = "The candidate is no longer available.";
                return false;
            }

            LastDownloadJobs = jobs;
            return true;
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

    public async Task<bool> CancelJobAsync(Guid jobId, CancellationToken cancellationToken = default)
        => await ExecuteJobActionAsync(
            () => apiClient.CancelJobAsync(jobId, cancellationToken),
            "The job is no longer available.");

    public async Task<bool> TryNextCandidateAsync(Guid jobId, CancellationToken cancellationToken = default)
        => await ExecuteJobActionAsync(
            () => apiClient.TryNextCandidateAsync(jobId, cancellationToken),
            "No next candidate is available.");

    private bool HasQuery()
        => Mode == DesktopSearchMode.Track
            ? !string.IsNullOrWhiteSpace(Artist) || !string.IsNullOrWhiteSpace(Title)
            : !string.IsNullOrWhiteSpace(Artist) || !string.IsNullOrWhiteSpace(Album);

    private static string? EmptyToNull(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void ClearResults()
    {
        FileCandidates = [];
        FolderCandidates = [];
        ResultsRevision = 0;
        IsResultsComplete = false;
    }

    private async Task<bool> ExecuteJobActionAsync(Func<Task<bool>> action, string notAvailableMessage)
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var succeeded = await action();
            if (!succeeded)
                ErrorMessage = notAvailableMessage;

            return succeeded;
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
}