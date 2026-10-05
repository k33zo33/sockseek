using System.Windows.Input;
using Sockseek.Api;

namespace Sockseek.Desktop;

public sealed class DesktopAboutViewModel : ObservableObject
{
    private readonly SockseekApiClient apiClient;
    private string productName = "Sockseek";
    private string version = "Unknown";
    private string commit = "Unknown";
    private string sourceUrl = "https://github.com/k33zo33/sockseek";
    private string license = "AGPL-3.0";
    private string errorMessage = string.Empty;
    private bool isBusy;
    private bool hasLoaded;

    public DesktopAboutViewModel(SockseekApiClient apiClient)
    {
        this.apiClient = apiClient;
        RefreshCommand = new DesktopAsyncCommand(() => RefreshAsync());
    }

    public ICommand RefreshCommand { get; }

    public string ProductName
    {
        get => productName;
        private set => SetProperty(ref productName, value);
    }

    public string Version
    {
        get => version;
        private set
        {
            if (!SetProperty(ref version, value))
                return;

            OnPropertyChanged(nameof(VersionSummary));
        }
    }

    public string Commit
    {
        get => commit;
        private set
        {
            if (!SetProperty(ref commit, value))
                return;

            OnPropertyChanged(nameof(VersionSummary));
        }
    }

    public string SourceUrl
    {
        get => sourceUrl;
        private set => SetProperty(ref sourceUrl, value);
    }

    public string License
    {
        get => license;
        private set => SetProperty(ref license, value);
    }

    public string VersionSummary => $"Version {Version} ({Commit})";

    public string LicenseSummary => "GNU AGPL-3.0; no warranty. Corresponding source is available from the listed source URL.";

    public string ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set => SetProperty(ref isBusy, value);
    }

    public void EnsureLoaded()
    {
        if (hasLoaded)
            return;

        hasLoaded = true;
        _ = RefreshAsync();
    }

    public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var info = await apiClient.GetSystemInfoAsync(cancellationToken);
            ProductName = info.Name;
            Version = info.Version;
            Commit = info.Commit;
            SourceUrl = info.SourceUrl;
            License = info.License;
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
