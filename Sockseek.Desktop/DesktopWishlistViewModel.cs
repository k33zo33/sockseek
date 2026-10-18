using System.Windows.Input;
using Sockseek.Api;

namespace Sockseek.Desktop;

public sealed class DesktopWishlistViewModel : ObservableObject
{
    private const int MinIntervalMinutes = 5;
    private const int MaxIntervalMinutes = 7 * 24 * 60;

    private readonly SockseekApiClient apiClient;
    private IReadOnlyList<DesktopWishlistEntryViewModel> entries = [];
    private DesktopWishlistEntryViewModel? selectedEntry;
    private string query = string.Empty;
    private string intervalMinutes = "30";
    private bool enabled = true;
    private bool isBusy;
    private string? errorMessage;
    private string? operationSummary;

    public DesktopWishlistViewModel(SockseekApiClient apiClient)
    {
        this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        RefreshCommand = new DesktopAsyncCommand(() => RefreshAsync());
        SaveCommand = new DesktopAsyncCommand(() => SaveAsync());
        ClearSelectionCommand = new DesktopCommand(ClearSelection);
        RunNowCommand = new DesktopAsyncParameterCommand<DesktopWishlistEntryViewModel>(entry => RunNowAsync(entry));
        ToggleEnabledCommand = new DesktopAsyncParameterCommand<DesktopWishlistEntryViewModel>(entry => ToggleEnabledAsync(entry));
        DeleteCommand = new DesktopAsyncParameterCommand<DesktopWishlistEntryViewModel>(entry => DeleteAsync(entry));
    }

    public ICommand RefreshCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand RunNowCommand { get; }
    public ICommand ToggleEnabledCommand { get; }
    public ICommand DeleteCommand { get; }

    public IReadOnlyList<DesktopWishlistEntryViewModel> Entries
    {
        get => entries;
        private set
        {
            if (!SetProperty(ref entries, value))
                return;

            OnPropertyChanged(nameof(HasEntries));
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    public bool HasEntries => Entries.Count > 0;
    public bool IsEmpty => Entries.Count == 0;

    public DesktopWishlistEntryViewModel? SelectedEntry
    {
        get => selectedEntry;
        set
        {
            if (!SetProperty(ref selectedEntry, value))
                return;

            if (value != null)
            {
                Query = value.Query;
                IntervalMinutes = value.IntervalMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture);
                Enabled = value.Enabled;
            }

            OnPropertyChanged(nameof(IsEditing));
            OnPropertyChanged(nameof(SaveActionLabel));
        }
    }

    public bool IsEditing => SelectedEntry != null;
    public string SaveActionLabel => IsEditing ? "Save" : "Add";

    public string Query
    {
        get => query;
        set => SetProperty(ref query, value ?? string.Empty);
    }

    public string IntervalMinutes
    {
        get => intervalMinutes;
        set => SetProperty(ref intervalMinutes, value ?? string.Empty);
    }

    public bool Enabled
    {
        get => enabled;
        set => SetProperty(ref enabled, value);
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

    public string? OperationSummary
    {
        get => operationSummary;
        private set => SetProperty(ref operationSummary, value);
    }

    public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
        => await ExecuteAsync(async () =>
        {
            await LoadEntriesAsync(cancellationToken);
            return true;
        }, cancellationToken);

    public async Task<SoulseekWishlistEntryDto?> SaveAsync(CancellationToken cancellationToken = default)
    {
        if (!TryCreateRequest(out var request))
            return null;

        SoulseekWishlistEntryDto? saved = null;
        bool succeeded = await ExecuteAsync(async () =>
        {
            saved = SelectedEntry == null
                ? await apiClient.CreateSoulseekWishlistEntryAsync(request, cancellationToken)
                : await apiClient.UpdateSoulseekWishlistEntryAsync(SelectedEntry.Id, request, cancellationToken);
            if (saved == null)
            {
                ErrorMessage = "The wishlist entry no longer exists.";
                return false;
            }

            OperationSummary = SelectedEntry == null ? "Wishlist entry added." : "Wishlist entry saved.";
            ClearSelection();
            await LoadEntriesAsync(cancellationToken);
            return true;
        }, cancellationToken);

        return succeeded ? saved : null;
    }

    public async Task<bool> RunNowAsync(
        DesktopWishlistEntryViewModel entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return await ExecuteAsync(async () =>
        {
            var started = await apiClient.RunSoulseekWishlistEntryNowAsync(entry.Id, cancellationToken);
            if (started == null)
            {
                ErrorMessage = "The wishlist entry no longer exists.";
                return false;
            }

            OperationSummary = $"Search started for {started.Query}.";
            await LoadEntriesAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    public async Task<bool> ToggleEnabledAsync(
        DesktopWishlistEntryViewModel entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return await ExecuteAsync(async () =>
        {
            var updated = await apiClient.UpdateSoulseekWishlistEntryAsync(
                entry.Id,
                new SaveSoulseekWishlistEntryRequestDto(
                    entry.Query,
                    entry.IntervalMinutes,
                    !entry.Enabled),
                cancellationToken);
            if (updated == null)
            {
                ErrorMessage = "The wishlist entry no longer exists.";
                return false;
            }

            OperationSummary = updated.Enabled ? "Wishlist entry enabled." : "Wishlist entry disabled.";
            if (SelectedEntry?.Id == entry.Id)
                ClearSelection();
            await LoadEntriesAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        DesktopWishlistEntryViewModel entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return await ExecuteAsync(async () =>
        {
            if (!await apiClient.DeleteSoulseekWishlistEntryAsync(entry.Id, cancellationToken))
            {
                ErrorMessage = "The wishlist entry no longer exists.";
                return false;
            }

            OperationSummary = "Wishlist entry deleted.";
            if (SelectedEntry?.Id == entry.Id)
                ClearSelection();
            await LoadEntriesAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    public void ClearSelection()
    {
        SelectedEntry = null;
        Query = string.Empty;
        IntervalMinutes = "30";
        Enabled = true;
    }

    private bool TryCreateRequest(out SaveSoulseekWishlistEntryRequestDto request)
    {
        request = null!;
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(Query))
        {
            ErrorMessage = "Enter a wishlist query.";
            return false;
        }

        if (!int.TryParse(IntervalMinutes.Trim(), out int parsedInterval)
            || parsedInterval is < MinIntervalMinutes or > MaxIntervalMinutes)
        {
            ErrorMessage = $"Interval must be between {MinIntervalMinutes} and {MaxIntervalMinutes} minutes.";
            return false;
        }

        request = new SaveSoulseekWishlistEntryRequestDto(Query.Trim(), parsedInterval, Enabled);
        return true;
    }

    private async Task LoadEntriesAsync(CancellationToken cancellationToken)
    {
        Guid? selectedId = SelectedEntry?.Id;
        Entries = (await apiClient.GetSoulseekWishlistAsync(cancellationToken))
            .Select(entry => new DesktopWishlistEntryViewModel(entry))
            .ToArray();

        if (selectedId is { } id)
        {
            var refreshedSelection = Entries.FirstOrDefault(entry => entry.Id == id);
            if (refreshedSelection == null)
                ClearSelection();
            else
                SelectedEntry = refreshedSelection;
        }
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

public sealed class DesktopWishlistEntryViewModel
{
    public DesktopWishlistEntryViewModel(SoulseekWishlistEntryDto entry)
    {
        Id = entry.Id;
        Query = entry.Query;
        IntervalMinutes = entry.IntervalMinutes;
        Enabled = entry.Enabled;
        SearchState = entry.SearchState;
        NextSearchAtUtc = entry.NextSearchAtUtc;
        LastSearchCompletedAtUtc = entry.LastSearchCompletedAtUtc;
        LastResultCount = entry.LastResultCount;
        LastErrorCode = entry.LastErrorCode;
    }

    public Guid Id { get; }
    public string Query { get; }
    public int IntervalMinutes { get; }
    public bool Enabled { get; }
    public string SearchState { get; }
    public DateTimeOffset? NextSearchAtUtc { get; }
    public DateTimeOffset? LastSearchCompletedAtUtc { get; }
    public int? LastResultCount { get; }
    public string? LastErrorCode { get; }
    public bool CanRun => Enabled && !StringComparer.OrdinalIgnoreCase.Equals(SearchState, "Searching");
    public string ToggleEnabledLabel => Enabled ? "Disable" : "Enable";
    public string IntervalSummary => $"Every {IntervalMinutes} min";

    public string StatusSummary
        => LastResultCount is { } count
            ? $"{SearchState} - {count} results"
            : LastErrorCode is { Length: > 0 } error
                ? $"{SearchState} - {error.Replace('_', ' ')}"
                : SearchState;

    public string ScheduleSummary
        => !Enabled
            ? "Disabled"
            : NextSearchAtUtc is { } next
                ? $"Next {next.ToLocalTime():yyyy-MM-dd HH:mm}"
                : "Ready";
}
