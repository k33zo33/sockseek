using System.Windows.Input;
using System.Globalization;
using Sockseek.Api;

namespace Sockseek.Desktop;

public sealed class DesktopSoulseekSharingViewModel : ObservableObject
{
    private readonly SockseekApiClient apiClient;
    private IReadOnlyList<DesktopSoulseekShareRootViewModel> roots = [];
    private IReadOnlyList<DesktopSoulseekUploadViewModel> uploads = [];
    private DesktopSoulseekShareRootViewModel? selectedRoot;
    private string rootPath = string.Empty;
    private string virtualName = string.Empty;
    private bool enabled = true;
    private long revision;
    private int directoryCount;
    private int fileCount;
    private bool isLoaded;
    private bool isBusy;
    private bool isUploadBusy;
    private bool uploadRefreshPending;
    private bool isUploadsLoaded;
    private long uploadRevision;
    private int activeUploadCount;
    private int terminalUploadCount;
    private int truncatedUploadCount;
    private string? errorMessage;
    private string? uploadErrorMessage;
    private string? operationSummary;

    public DesktopSoulseekSharingViewModel(SockseekApiClient apiClient)
    {
        this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        RefreshCommand = new DesktopAsyncCommand(() => RefreshAsync());
        RefreshUploadsCommand = new DesktopAsyncCommand(() => RefreshUploadsAsync());
        RebuildCommand = new DesktopAsyncCommand(() => RebuildAsync());
        SaveCommand = new DesktopAsyncCommand(() => SaveAsync());
        ClearSelectionCommand = new DesktopCommand(ClearSelection);
        EditCommand = new DesktopCommandParameter<DesktopSoulseekShareRootViewModel>(root => SelectedRoot = root);
        ToggleEnabledCommand = new DesktopAsyncParameterCommand<DesktopSoulseekShareRootViewModel>(root => ToggleEnabledAsync(root));
        DeleteCommand = new DesktopAsyncParameterCommand<DesktopSoulseekShareRootViewModel>(root => DeleteAsync(root));
    }

    public ICommand RefreshCommand { get; }

    public ICommand RefreshUploadsCommand { get; }

    public ICommand RebuildCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand ClearSelectionCommand { get; }

    public ICommand EditCommand { get; }

    public ICommand ToggleEnabledCommand { get; }

    public ICommand DeleteCommand { get; }

    public IReadOnlyList<DesktopSoulseekShareRootViewModel> Roots
    {
        get => roots;
        private set
        {
            if (!SetProperty(ref roots, value))
                return;

            OnPropertyChanged(nameof(HasRoots));
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    public IReadOnlyList<DesktopSoulseekUploadViewModel> Uploads
    {
        get => uploads;
        private set
        {
            if (!SetProperty(ref uploads, value))
                return;
            OnPropertyChanged(nameof(HasUploads));
            OnPropertyChanged(nameof(IsUploadsEmpty));
        }
    }

    public DesktopSoulseekShareRootViewModel? SelectedRoot
    {
        get => selectedRoot;
        set
        {
            if (!SetProperty(ref selectedRoot, value))
                return;

            if (value is not null)
            {
                RootPath = value.Path;
                VirtualName = value.VirtualName;
                Enabled = value.Enabled;
            }

            OnPropertyChanged(nameof(IsEditing));
            OnPropertyChanged(nameof(SaveActionLabel));
        }
    }

    public string RootPath
    {
        get => rootPath;
        set => SetProperty(ref rootPath, value ?? string.Empty);
    }

    public string VirtualName
    {
        get => virtualName;
        set => SetProperty(ref virtualName, value ?? string.Empty);
    }

    public bool Enabled
    {
        get => enabled;
        set => SetProperty(ref enabled, value);
    }

    public long Revision
    {
        get => revision;
        private set
        {
            if (SetProperty(ref revision, value))
                OnPropertyChanged(nameof(SnapshotSummary));
        }
    }

    public int DirectoryCount
    {
        get => directoryCount;
        private set
        {
            if (SetProperty(ref directoryCount, value))
                OnPropertyChanged(nameof(SnapshotSummary));
        }
    }

    public int FileCount
    {
        get => fileCount;
        private set
        {
            if (SetProperty(ref fileCount, value))
                OnPropertyChanged(nameof(SnapshotSummary));
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
            OnPropertyChanged(nameof(SnapshotSummary));
        }
    }

    public bool IsBusy
    {
        get => isBusy;
        private set => SetProperty(ref isBusy, value);
    }

    public bool IsUploadBusy
    {
        get => isUploadBusy;
        private set => SetProperty(ref isUploadBusy, value);
    }

    public bool IsUploadsLoaded
    {
        get => isUploadsLoaded;
        private set
        {
            if (!SetProperty(ref isUploadsLoaded, value))
                return;
            OnPropertyChanged(nameof(IsUploadsEmpty));
            OnPropertyChanged(nameof(UploadSummary));
        }
    }

    public long UploadRevision
    {
        get => uploadRevision;
        private set
        {
            if (SetProperty(ref uploadRevision, value))
                OnPropertyChanged(nameof(UploadSummary));
        }
    }

    public int ActiveUploadCount
    {
        get => activeUploadCount;
        private set
        {
            if (SetProperty(ref activeUploadCount, value))
                OnPropertyChanged(nameof(UploadSummary));
        }
    }

    public int TerminalUploadCount
    {
        get => terminalUploadCount;
        private set
        {
            if (SetProperty(ref terminalUploadCount, value))
                OnPropertyChanged(nameof(UploadSummary));
        }
    }

    public int TruncatedUploadCount
    {
        get => truncatedUploadCount;
        private set
        {
            if (SetProperty(ref truncatedUploadCount, value))
                OnPropertyChanged(nameof(UploadSummary));
        }
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    public string? UploadErrorMessage
    {
        get => uploadErrorMessage;
        private set => SetProperty(ref uploadErrorMessage, value);
    }

    public string? OperationSummary
    {
        get => operationSummary;
        private set => SetProperty(ref operationSummary, value);
    }

    public bool HasRoots => Roots.Count > 0;

    public bool IsEmpty => IsLoaded && !HasRoots;

    public bool IsEditing => SelectedRoot is not null;

    public bool HasUploads => Uploads.Count > 0;

    public bool IsUploadsEmpty => IsUploadsLoaded && !HasUploads;

    public string SaveActionLabel => IsEditing ? "Save" : "Add";

    public string SnapshotSummary
        => IsLoaded
            ? $"{FileCount} files across {DirectoryCount} folders | revision {Revision}"
            : "Share snapshot not loaded";

    public string UploadSummary
        => IsUploadsLoaded
            ? $"{ActiveUploadCount} active | {TerminalUploadCount} recent"
                + (TruncatedUploadCount > 0 ? $" | {TruncatedUploadCount} omitted" : string.Empty)
                + $" | revision {UploadRevision}"
            : "Upload state not loaded";

    public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
    {
        bool sharingRefreshed = await ExecuteAsync(async () =>
        {
            ApplySnapshot(await apiClient.GetSoulseekSharingAsync(cancellationToken));
            return true;
        }, cancellationToken);
        bool uploadsRefreshed = await RefreshUploadsAsync(cancellationToken);
        return sharingRefreshed && uploadsRefreshed;
    }

    public async Task<bool> RefreshUploadsAsync(CancellationToken cancellationToken = default)
    {
        if (IsUploadBusy)
        {
            uploadRefreshPending = true;
            return false;
        }

        UploadErrorMessage = null;
        IsUploadBusy = true;
        try
        {
            ApplyUploadSnapshot(await apiClient.GetSoulseekUploadsAsync(cancellationToken));
            return true;
        }
        catch (SockseekApiRequestException exception)
        {
            UploadErrorMessage = exception.Message;
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            UploadErrorMessage = "The local daemon did not respond in time. Retry the operation.";
            return false;
        }
        catch (HttpRequestException)
        {
            UploadErrorMessage = "The local daemon is unavailable. Wait for it to reconnect, then retry.";
            return false;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            UploadErrorMessage = "The local daemon returned an invalid upload response. Retry the operation.";
            return false;
        }
        finally
        {
            IsUploadBusy = false;
            bool shouldRefresh = uploadRefreshPending;
            uploadRefreshPending = false;
            if (shouldRefresh && !cancellationToken.IsCancellationRequested)
                _ = RefreshUploadsAsync();
        }
    }

    public async Task<bool> RebuildAsync(CancellationToken cancellationToken = default)
        => await ExecuteAsync(async () =>
        {
            ApplySnapshot(await apiClient.RebuildSoulseekSharingAsync(cancellationToken));
            OperationSummary = "Share snapshot rebuilt.";
            return true;
        }, cancellationToken);

    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        if (!TryCreateRequest(out var request))
            return false;

        bool editing = SelectedRoot is not null;
        return await ExecuteAsync(async () =>
        {
            var snapshot = SelectedRoot is null
                ? await apiClient.CreateSoulseekShareRootAsync(request, cancellationToken)
                : await apiClient.UpdateSoulseekShareRootAsync(SelectedRoot.Id, request, cancellationToken);
            if (snapshot is null)
            {
                ErrorMessage = "The share root no longer exists.";
                return false;
            }

            ClearSelection();
            ApplySnapshot(snapshot);
            OperationSummary = editing ? "Share root saved." : "Share root added.";
            return true;
        }, cancellationToken);
    }

    public async Task<bool> ToggleEnabledAsync(
        DesktopSoulseekShareRootViewModel root,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        return await ExecuteAsync(async () =>
        {
            var snapshot = await apiClient.UpdateSoulseekShareRootAsync(
                root.Id,
                new SaveSoulseekShareRootRequestDto(root.Path, root.VirtualName, !root.Enabled),
                cancellationToken);
            if (snapshot is null)
            {
                ErrorMessage = "The share root no longer exists.";
                return false;
            }

            if (SelectedRoot?.Id == root.Id)
                ClearSelection();
            ApplySnapshot(snapshot);
            OperationSummary = root.Enabled ? "Share root disabled." : "Share root enabled.";
            return true;
        }, cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        DesktopSoulseekShareRootViewModel root,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        return await ExecuteAsync(async () =>
        {
            if (!await apiClient.DeleteSoulseekShareRootAsync(root.Id, cancellationToken))
            {
                ErrorMessage = "The share root no longer exists.";
                return false;
            }

            if (SelectedRoot?.Id == root.Id)
                ClearSelection();
            ApplySnapshot(await apiClient.GetSoulseekSharingAsync(cancellationToken));
            OperationSummary = "Share root removed. Local audio was not deleted.";
            return true;
        }, cancellationToken);
    }

    public void ClearSelection()
    {
        SelectedRoot = null;
        RootPath = string.Empty;
        VirtualName = string.Empty;
        Enabled = true;
    }

    private bool TryCreateRequest(out SaveSoulseekShareRootRequestDto request)
    {
        request = null!;
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(RootPath))
        {
            ErrorMessage = "Enter a local share root path.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(VirtualName))
        {
            ErrorMessage = "Enter a virtual share name.";
            return false;
        }

        request = new SaveSoulseekShareRootRequestDto(RootPath.Trim(), VirtualName.Trim(), Enabled);
        return true;
    }

    private void ApplySnapshot(SoulseekSharingSnapshotDto snapshot)
    {
        Guid? selectedId = SelectedRoot?.Id;
        Revision = snapshot.Revision;
        DirectoryCount = snapshot.DirectoryCount;
        FileCount = snapshot.FileCount;
        Roots = snapshot.Roots.Select(root => new DesktopSoulseekShareRootViewModel(root)).ToArray();
        IsLoaded = true;

        if (selectedId is not { } id)
            return;

        var refreshed = Roots.FirstOrDefault(root => root.Id == id);
        if (refreshed is null)
            ClearSelection();
        else
            SelectedRoot = refreshed;
    }

    private void ApplyUploadSnapshot(SoulseekUploadSnapshotDto snapshot)
    {
        UploadRevision = snapshot.Revision;
        ActiveUploadCount = snapshot.ActiveCount;
        TerminalUploadCount = snapshot.TerminalCount;
        TruncatedUploadCount = snapshot.TruncatedCount;
        Uploads = snapshot.Uploads.Select(upload => new DesktopSoulseekUploadViewModel(upload)).ToArray();
        IsUploadsLoaded = true;
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

public sealed class DesktopSoulseekShareRootViewModel(SoulseekShareRootDto root)
{
    public Guid Id { get; } = root.Id;

    public string Path { get; } = root.Path;

    public string VirtualName { get; } = root.VirtualName;

    public bool Enabled { get; } = root.Enabled;

    public bool Included { get; } = root.Included;

    public string StatusCode { get; } = root.StatusCode;

    public int DirectoryCount { get; } = root.DirectoryCount;

    public int FileCount { get; } = root.FileCount;

    public string ToggleEnabledLabel => Enabled ? "Disable" : "Enable";

    public string StatusSummary
        => $"{StatusCode.Replace('_', ' ')} | {FileCount} files / {DirectoryCount} folders";
}

public sealed class DesktopSoulseekUploadViewModel(SoulseekUploadDto upload)
{
    public Guid Id { get; } = upload.Id;
    public string Username { get; } = upload.Username;
    public string Filename { get; } = upload.Filename;
    public string State { get; } = upload.State;
    public long Size { get; } = upload.Size;
    public long BytesTransferred { get; } = upload.BytesTransferred;
    public double BytesPerSecond { get; } = upload.BytesPerSecond;
    public DateTimeOffset? StartedAtUtc { get; } = upload.StartedAtUtc;
    public DateTimeOffset? EndedAtUtc { get; } = upload.EndedAtUtc;
    public bool IsTerminal { get; } = upload.IsTerminal;

    public string PeerSummary => $"To {Username}";

    public string StateSummary => State.Replace(", ", " / ", StringComparison.Ordinal);

    public string ProgressSummary
    {
        get
        {
            double percent = Size > 0 ? Math.Clamp((double)BytesTransferred / Size * 100, 0, 100) : 0;
            return $"{FormatBytes(BytesTransferred)} / {FormatBytes(Size)} | {percent:0}% | {FormatBytesPerSecond(BytesPerSecond)}";
        }
    }

    public string TimeSummary
        => EndedAtUtc is { } ended
            ? $"Finished {ended.ToLocalTime():g}"
            : StartedAtUtc is { } started
                ? $"Started {started.ToLocalTime():g}"
                : "Waiting to start";

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024)
            return string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024d * 1024 * 1024):0.0} GB");
        if (bytes >= 1024L * 1024)
            return string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024d * 1024):0.0} MB");
        if (bytes >= 1024)
            return string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024d:0.0} KB");
        return $"{bytes} B";
    }

    private static string FormatBytesPerSecond(double bytesPerSecond)
        => $"{FormatBytes((long)Math.Max(0, bytesPerSecond))}/s";
}
