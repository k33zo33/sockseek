using System.Windows.Input;
using Sockseek.Api;

namespace Sockseek.Desktop;

public sealed class DesktopDownloadQueueViewModel : ObservableObject
{
    private readonly SockseekApiClient apiClient;
    private IReadOnlyList<JobSummaryDto> jobs = [];
    private bool isBusy;
    private string? errorMessage;

    public DesktopDownloadQueueViewModel(SockseekApiClient apiClient)
    {
        this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        RefreshCommand = new DesktopAsyncCommand(() => RefreshAsync());
    }

    public ICommand RefreshCommand { get; }

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
}