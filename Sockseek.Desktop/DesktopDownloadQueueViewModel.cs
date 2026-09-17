using System.Windows.Input;
using Sockseek.Api;

namespace Sockseek.Desktop;

public sealed class DesktopDownloadQueueViewModel : ObservableObject
{
    private readonly SockseekApiClient apiClient;
    private readonly IDesktopFileOpener fileOpener;
    private IReadOnlyList<JobSummaryDto> jobs = [];
    private bool isBusy;
    private string? errorMessage;
    private IReadOnlyList<DownloadProgressEventDto> progress = [];
    private IReadOnlyList<DesktopNotificationViewModel> notifications = [];
    private string? selectedWorkflowTitle;
    private IReadOnlyList<DesktopWorkflowTreeNodeViewModel> selectedWorkflowNodes = [];

    public DesktopDownloadQueueViewModel(SockseekApiClient apiClient, IDesktopFileOpener? fileOpener = null)
    {
        this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        this.fileOpener = fileOpener ?? new SystemDesktopFileOpener();
        RefreshCommand = new DesktopAsyncCommand(() => RefreshAsync());
        CancelCommand = new DesktopAsyncParameterCommand<Guid>(jobId => CancelAsync(jobId));
        NextCandidateCommand = new DesktopAsyncParameterCommand<Guid>(jobId => NextCandidateAsync(jobId));
        RetryCommand = new DesktopAsyncParameterCommand<Guid>(jobId => RetryAsync(jobId));
        OpenFileCommand = new DesktopAsyncParameterCommand<Guid>(jobId => OpenFileAsync(jobId));
        OpenFolderCommand = new DesktopAsyncParameterCommand<Guid>(jobId => OpenFolderAsync(jobId));
        LoadWorkflowTreeCommand = new DesktopAsyncParameterCommand<JobSummaryDto>(job => LoadWorkflowTreeAsync(job));
    }

    public ICommand RefreshCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand NextCandidateCommand { get; }

    public ICommand RetryCommand { get; }

    public ICommand OpenFileCommand { get; }

    public ICommand OpenFolderCommand { get; }

    public ICommand LoadWorkflowTreeCommand { get; }

    public IReadOnlyList<JobSummaryDto> Jobs
    {
        get => jobs;
        private set => SetProperty(ref jobs, value);
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

    public IReadOnlyList<DownloadProgressEventDto> Progress
    {
        get => progress;
        private set => SetProperty(ref progress, value);
    }

    public IReadOnlyList<DesktopNotificationViewModel> Notifications
    {
        get => notifications;
        private set => SetProperty(ref notifications, value);
    }

    public string? SelectedWorkflowTitle
    {
        get => selectedWorkflowTitle;
        private set => SetProperty(ref selectedWorkflowTitle, value);
    }

    public IReadOnlyList<DesktopWorkflowTreeNodeViewModel> SelectedWorkflowNodes
    {
        get => selectedWorkflowNodes;
        private set
        {
            if (SetProperty(ref selectedWorkflowNodes, value))
                OnPropertyChanged(nameof(IsWorkflowTreeVisible));
        }
    }

    public bool IsWorkflowTreeVisible => SelectedWorkflowNodes.Count > 0;

    public void ApplyWorkflowUpdate(WorkflowUpdateBatchDto batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        var updates = Progress.ToDictionary(item => item.JobId);
        foreach (var update in batch.Progress)
            updates[update.JobId] = update;
        Progress = updates.Values.ToArray();

        var newNotifications = BuildNotifications(batch).ToArray();
        if (newNotifications.Length > 0)
            Notifications = newNotifications.Concat(Notifications).Take(20).ToArray();
    }

    public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            Jobs = await apiClient.GetJobsAsync(new JobQuery(null, null, null, null, IncludeAll: true), cancellationToken);
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

    public Task<bool> CancelAsync(Guid jobId, CancellationToken cancellationToken = default)
        => ExecuteActionAsync(() => apiClient.CancelJobAsync(jobId, cancellationToken));

    public Task<bool> NextCandidateAsync(Guid jobId, CancellationToken cancellationToken = default)
        => ExecuteActionAsync(() => apiClient.TryNextCandidateAsync(jobId, cancellationToken));

    public Task<bool> RetryAsync(Guid jobId, CancellationToken cancellationToken = default)
        => ExecuteActionAsync(() => apiClient.RetryJobAsync(jobId, cancellationToken));

    public Task<bool> OpenFileAsync(Guid jobId, CancellationToken cancellationToken = default)
        => OpenDownloadedPathAsync(
            jobId,
            path => fileOpener.OpenFileAsync(path, cancellationToken),
            "The downloaded file is not available.",
            cancellationToken);

    public Task<bool> OpenFolderAsync(Guid jobId, CancellationToken cancellationToken = default)
        => OpenDownloadedPathAsync(
            jobId,
            path => fileOpener.OpenFolderAsync(path, cancellationToken),
            "The downloaded folder is not available.",
            cancellationToken);

    public async Task<bool> LoadWorkflowTreeAsync(JobSummaryDto job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var tree = await apiClient.GetWorkflowTreeAsync(job.WorkflowId, cancellationToken);
            if (tree is null)
            {
                SelectedWorkflowTitle = null;
                SelectedWorkflowNodes = [];
                ErrorMessage = "The workflow is no longer available.";
                return false;
            }

            SelectedWorkflowTitle = $"{tree.Summary.Title} ({tree.Summary.State})";
            SelectedWorkflowNodes = FlattenWorkflowTree(tree.Jobs).ToArray();
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

    private async Task<bool> ExecuteActionAsync(Func<Task<bool>> action)
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var succeeded = await action();
            if (!succeeded)
                ErrorMessage = "The download job is no longer available.";
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

    private async Task<bool> OpenDownloadedPathAsync(
        Guid jobId,
        Func<string, Task<bool>> openAsync,
        string unavailableMessage,
        CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var detail = await apiClient.GetJobDetailAsync(jobId, cancellationToken);
            var path = GetDownloadPath(detail);
            if (string.IsNullOrWhiteSpace(path) || !await openAsync(path))
            {
                ErrorMessage = unavailableMessage;
                return false;
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

    private static string? GetDownloadPath(JobDetailDto? detail)
        => detail?.Payload switch
        {
            SongJobPayloadDto song => song.DownloadPath,
            AlbumJobPayloadDto album => album.DownloadPath,
            _ => null,
        };

    private static IEnumerable<DesktopWorkflowTreeNodeViewModel> FlattenWorkflowTree(
        IEnumerable<WorkflowJobNodeDto> nodes,
        int depth = 0)
    {
        foreach (var node in nodes)
        {
            yield return new DesktopWorkflowTreeNodeViewModel(node.Summary, depth);
            foreach (var child in FlattenWorkflowTree(node.Children, depth + 1))
                yield return child;
        }
    }

    private static IEnumerable<DesktopNotificationViewModel> BuildNotifications(WorkflowUpdateBatchDto batch)
    {
        foreach (var job in batch.JobUpserts)
        {
            if (job.LifecycleState != ServerJobLifecycleState.Terminal)
                continue;

            var title = job.TerminalOutcome switch
            {
                ServerJobTerminalOutcome.Succeeded => "Download completed",
                ServerJobTerminalOutcome.Failed => "Download failed",
                ServerJobTerminalOutcome.Cancelled => "Download cancelled",
                ServerJobTerminalOutcome.Skipped => "Download skipped",
                ServerJobTerminalOutcome.PartialSuccess => "Download partially completed",
                _ => "Download finished",
            };
            var severity = job.TerminalOutcome switch
            {
                ServerJobTerminalOutcome.Succeeded => "success",
                ServerJobTerminalOutcome.Failed or ServerJobTerminalOutcome.Cancelled => "error",
                _ => "info",
            };

            yield return new DesktopNotificationViewModel(title, DescribeJob(job), severity);
        }

        foreach (var activity in batch.Activity)
        {
            if (activity.Category != "activity")
                continue;

            yield return new DesktopNotificationViewModel(
                ActivityTitle(activity.Type),
                activity.Type,
                activity.Category == "progress" ? "info" : "activity");
        }
    }

    private static string DescribeJob(JobSummaryDto job)
    {
        var name = string.IsNullOrWhiteSpace(job.ItemName)
            ? job.QueryText
            : job.ItemName;
        return string.IsNullOrWhiteSpace(name)
            ? $"#{job.DisplayId} {job.Kind}"
            : $"#{job.DisplayId} {job.Kind}: {name}";
    }

    private static string ActivityTitle(string type)
        => type switch
        {
            "download.started" => "Download started",
            "download.attempt-failed" => "Download attempt failed",
            "search.updated" => "Search results updated",
            "job.upserted" => "Job updated",
            _ => "Workflow activity",
        };
}
