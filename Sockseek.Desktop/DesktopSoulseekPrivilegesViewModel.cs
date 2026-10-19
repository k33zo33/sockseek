using System.Windows.Input;
using Sockseek.Api;

namespace Sockseek.Desktop;

public sealed class DesktopSoulseekPrivilegesViewModel : ObservableObject
{
    private readonly SockseekApiClient apiClient;
    private IReadOnlyList<DesktopSoulseekPrivilegeNotificationViewModel> notifications = [];
    private int? remainingDays;
    private DateTimeOffset? refreshedAtUtc;
    private bool isLoaded;
    private bool isBusy;
    private string? errorMessage;
    private string? operationSummary;

    public DesktopSoulseekPrivilegesViewModel(SockseekApiClient apiClient)
    {
        this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        RefreshCommand = new DesktopAsyncCommand(() => RefreshAsync());
        AcknowledgeCommand = new DesktopAsyncParameterCommand<DesktopSoulseekPrivilegeNotificationViewModel>(
            notification => AcknowledgeAsync(notification));
    }

    public ICommand RefreshCommand { get; }

    public ICommand AcknowledgeCommand { get; }

    public IReadOnlyList<DesktopSoulseekPrivilegeNotificationViewModel> Notifications
    {
        get => notifications;
        private set
        {
            if (!SetProperty(ref notifications, value))
                return;

            OnPropertyChanged(nameof(HasNotifications));
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    public int? RemainingDays
    {
        get => remainingDays;
        private set
        {
            if (!SetProperty(ref remainingDays, value))
                return;

            OnPropertyChanged(nameof(StatusSummary));
        }
    }

    public DateTimeOffset? RefreshedAtUtc
    {
        get => refreshedAtUtc;
        private set
        {
            if (!SetProperty(ref refreshedAtUtc, value))
                return;

            OnPropertyChanged(nameof(RefreshedSummary));
        }
    }

    public bool IsLoaded
    {
        get => isLoaded;
        private set
        {
            if (!SetProperty(ref isLoaded, value))
                return;

            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(StatusSummary));
        }
    }

    public bool IsBusy
    {
        get => isBusy;
        private set => SetProperty(ref isBusy, value);
    }

    public bool HasNotifications => Notifications.Count > 0;

    public bool IsEmpty => IsLoaded && Notifications.Count == 0;

    public string StatusSummary
        => !IsLoaded || RemainingDays is null
            ? "Privilege status not loaded"
            : RemainingDays == 0
                ? "No privilege days remaining"
                : RemainingDays == 1
                    ? "1 privilege day remaining"
                    : $"{RemainingDays} privilege days remaining";

    public string? RefreshedSummary
        => RefreshedAtUtc is { } refreshed
            ? $"Updated {refreshed.ToLocalTime():yyyy-MM-dd HH:mm}"
            : null;

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
            await LoadStatusAsync(cancellationToken);
            return true;
        }, cancellationToken);

    public async Task<bool> AcknowledgeAsync(
        DesktopSoulseekPrivilegeNotificationViewModel notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (!notification.RequiresAcknowledgement)
        {
            ErrorMessage = "This privilege notification does not require acknowledgement.";
            return false;
        }

        return await ExecuteAsync(async () =>
        {
            if (!await apiClient.AcknowledgeSoulseekPrivilegeNotificationAsync(notification.Id, cancellationToken))
            {
                ErrorMessage = "The privilege notification no longer exists.";
                await LoadStatusAsync(cancellationToken);
                return false;
            }

            OperationSummary = "Privilege notification acknowledged.";
            await LoadStatusAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    private async Task LoadStatusAsync(CancellationToken cancellationToken)
    {
        var status = await apiClient.GetSoulseekPrivilegeStatusAsync(cancellationToken);
        RemainingDays = status.RemainingDays;
        RefreshedAtUtc = status.RefreshedAtUtc;
        Notifications = status.Notifications
            .Select(notification => new DesktopSoulseekPrivilegeNotificationViewModel(notification))
            .ToArray();
        IsLoaded = true;
    }

    private async Task<bool> ExecuteAsync(Func<Task<bool>> action, CancellationToken cancellationToken)
    {
        if (IsBusy)
            return false;

        ErrorMessage = null;
        OperationSummary = null;
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
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            ErrorMessage = "The local daemon did not respond in time. Retry the operation.";
            return false;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "The local daemon is unavailable. Wait for it to reconnect, then retry.";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed class DesktopSoulseekPrivilegeNotificationViewModel
{
    public DesktopSoulseekPrivilegeNotificationViewModel(SoulseekPrivilegeNotificationDto notification)
    {
        Id = notification.Id;
        Username = notification.Username;
        ReceivedAtUtc = notification.ReceivedAtUtc;
        RequiresAcknowledgement = notification.RequiresAcknowledgement;
    }

    public Guid Id { get; }

    public string Username { get; }

    public DateTimeOffset ReceivedAtUtc { get; }

    public bool RequiresAcknowledgement { get; }

    public string ReceivedSummary => ReceivedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public string AcknowledgementSummary
        => RequiresAcknowledgement ? "Acknowledgement required" : "Recognized";
}
