using Sockseek.Api;
using System.Windows.Input;

namespace Sockseek.Desktop;

public sealed class DesktopSearchViewModel : ObservableObject
{
    private readonly SockseekApiClient apiClient;
    private DesktopSearchMode mode = DesktopSearchMode.Track;
    private string artist = string.Empty;
    private string title = string.Empty;
    private string album = string.Empty;
    private string searchHint = string.Empty;
    private string profileNames = string.Empty;
    private string minBitrate = string.Empty;
    private string formats = string.Empty;
    private bool isBusy;
    private JobSummaryDto? lastJob;
    private string? errorMessage;
    private IReadOnlyList<DesktopFileCandidateViewModel> fileCandidates = [];
    private IReadOnlyList<DesktopAlbumFolderViewModel> folderCandidates = [];
    private IReadOnlyList<JobSummaryDto> lastDownloadJobs = [];
    private int resultsRevision;
    private bool isResultsComplete;

    public DesktopSearchViewModel(SockseekApiClient apiClient)
    {
        this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        SearchCommand = new DesktopAsyncCommand(() => SearchAsync());
        RefreshResultsCommand = new DesktopAsyncCommand(() => RefreshResultsAsync());
        DownloadFileCommand = new DesktopAsyncParameterCommand<DesktopFileCandidateViewModel>(candidate => DownloadFileAsync(candidate.Candidate));
        DownloadFolderCommand = new DesktopAsyncParameterCommand<DesktopAlbumFolderViewModel>(folder => DownloadFolderAsync(folder.Folder));
    }

    public ICommand SearchCommand { get; }

    public ICommand RefreshResultsCommand { get; }

    public ICommand DownloadFileCommand { get; }

    public ICommand DownloadFolderCommand { get; }

    public DesktopSearchMode Mode
    {
        get => mode;
        set
        {
            if (!SetProperty(ref mode, value))
                return;

            OnPropertyChanged(nameof(IsTrackMode));
            OnPropertyChanged(nameof(IsAlbumMode));
        }
    }

    public bool IsTrackMode
    {
        get => Mode == DesktopSearchMode.Track;
        set
        {
            if (value)
                Mode = DesktopSearchMode.Track;
        }
    }

    public bool IsAlbumMode
    {
        get => Mode == DesktopSearchMode.Album;
        set
        {
            if (value)
                Mode = DesktopSearchMode.Album;
        }
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

    public string ProfileNames
    {
        get => profileNames;
        set => SetProperty(ref profileNames, value ?? string.Empty);
    }

    public string MinBitrate
    {
        get => minBitrate;
        set => SetProperty(ref minBitrate, value ?? string.Empty);
    }

    public string Formats
    {
        get => formats;
        set => SetProperty(ref formats, value ?? string.Empty);
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

    public IReadOnlyList<DesktopFileCandidateViewModel> FileCandidates
    {
        get => fileCandidates;
        private set => SetProperty(ref fileCandidates, value);
    }

    public IReadOnlyList<DesktopAlbumFolderViewModel> FolderCandidates
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

        if (!TryBuildSearchSubmissionOptions(out var options))
            return null;

        IsBusy = true;
        try
        {
            LastJob = Mode == DesktopSearchMode.Track
                ? await apiClient.SubmitTrackSearchJobAsync(
                    new SongQueryDto(
                        EmptyToNull(Artist),
                        EmptyToNull(Title),
                        EmptyToNull(Album)),
                    options,
                    cancellationToken)
                : await apiClient.SubmitAlbumSearchJobAsync(
                    new AlbumQueryDto(
                        EmptyToNull(Artist),
                        EmptyToNull(Album),
                        EmptyToNull(SearchHint)),
                    options,
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

                FileCandidates = snapshot.Items.Select(candidate => new DesktopFileCandidateViewModel(candidate)).ToArray();
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

                FolderCandidates = snapshot.Items.Select(folder => new DesktopAlbumFolderViewModel(folder)).ToArray();
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

    public async Task<bool> DownloadFolderAsync(AlbumFolderDto folder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ErrorMessage = null;
        if (LastJob is null)
        {
            ErrorMessage = "Start a search before downloading a folder.";
            return false;
        }

        IsBusy = true;
        try
        {
            var job = await apiClient.StartFolderDownloadAsync(
                LastJob.JobId,
                new StartFolderDownloadRequestDto(folder.Ref, AlbumQuery: new AlbumQueryDto(Artist, Album)),
                cancellationToken);
            if (job is null)
            {
                ErrorMessage = "The album folder is no longer available.";
                return false;
            }

            LastDownloadJobs = [job];
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

    private bool TryBuildSearchSubmissionOptions(out SearchSubmissionOptionsDto? options)
    {
        options = null;
        var minBitrateValue = ParseNullablePositiveInt(MinBitrate, "Minimum bitrate");
        if (minBitrateValue == InvalidNumber)
            return false;

        var profileNameList = SplitList(ProfileNames).ToArray();
        var formatList = SplitList(Formats)
            .Select(format => format.TrimStart('.').ToLowerInvariant())
            .Where(format => format.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (profileNameList.Length == 0 && minBitrateValue is null && formatList.Length == 0)
            return true;

        options = new SearchSubmissionOptionsDto(
            profileNameList.Length == 0 ? null : profileNameList,
            minBitrateValue,
            formatList.Length == 0 ? null : formatList);
        return true;
    }

    private const int InvalidNumber = -1;

    private int? ParseNullablePositiveInt(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (int.TryParse(value.Trim(), out var parsed) && parsed > 0)
            return parsed;

        ErrorMessage = $"{label} must be a positive number.";
        return InvalidNumber;
    }

    private static IEnumerable<string> SplitList(string value)
        => value.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
