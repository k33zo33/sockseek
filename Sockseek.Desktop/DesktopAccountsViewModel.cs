using System.Windows.Input;
using Sockseek.Api;

namespace Sockseek.Desktop;

public sealed class DesktopAccountsViewModel : ObservableObject
{
    private readonly SockseekApiClient apiClient;
    private IReadOnlyList<ProviderConnectionCardViewModel> providerCards = [];
    private IReadOnlyList<DesktopExternalAccountViewModel> accounts = [];
    private SoulseekAccountDto? soulseekAccount;
    private string soulseekUsername = string.Empty;
    private string soulseekPassword = string.Empty;
    private bool isBusy;
    private string? errorMessage;

    public DesktopAccountsViewModel(SockseekApiClient apiClient)
    {
        this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        RefreshCommand = new DesktopAsyncCommand(() => RefreshAsync());
        SaveSoulseekAccountCommand = new DesktopAsyncCommand(() => SaveSoulseekAccountAsync());
        DisconnectSoulseekAccountCommand = new DesktopAsyncCommand(() => DisconnectSoulseekAccountAsync());
        DisconnectCommand = new DesktopAsyncParameterCommand<Guid>(accountId => DisconnectAsync(accountId));
    }

    public ICommand RefreshCommand { get; }

    public ICommand SaveSoulseekAccountCommand { get; }

    public ICommand DisconnectSoulseekAccountCommand { get; }

    public ICommand DisconnectCommand { get; }

    public SoulseekAccountDto? SoulseekAccount
    {
        get => soulseekAccount;
        private set
        {
            if (!SetProperty(ref soulseekAccount, value))
                return;

            OnPropertyChanged(nameof(SoulseekStatusSummary));
            OnPropertyChanged(nameof(CanDisconnectSoulseekAccount));
        }
    }

    public string SoulseekUsername
    {
        get => soulseekUsername;
        set
        {
            if (SetProperty(ref soulseekUsername, value ?? string.Empty))
                OnPropertyChanged(nameof(CanSaveSoulseekAccount));
        }
    }

    public string SoulseekPassword
    {
        get => soulseekPassword;
        set
        {
            if (SetProperty(ref soulseekPassword, value ?? string.Empty))
                OnPropertyChanged(nameof(CanSaveSoulseekAccount));
        }
    }

    public bool CanSaveSoulseekAccount
        => !IsBusy
            && !string.IsNullOrWhiteSpace(SoulseekUsername)
            && SoulseekPassword.Length > 0;

    public bool CanDisconnectSoulseekAccount
        => !IsBusy
            && SoulseekAccount is { } account
            && (account.IsConfigured || account.CredentialStatus == "secret_unavailable");

    public string SoulseekStatusSummary
        => SoulseekAccount switch
        {
            { IsConnected: true, Username: { Length: > 0 } username } => $"Connected as {username}",
            { IsConfigured: true, Username: { Length: > 0 } username } account =>
                $"Configured as {username} - {account.ConnectionState}",
            { CredentialStatus: "secret_unavailable", Username: { Length: > 0 } username } =>
                $"{username} needs credentials again",
            _ => "Not configured",
        };

    public IReadOnlyList<ProviderConnectionCardViewModel> ProviderCards
    {
        get => providerCards;
        private set => SetProperty(ref providerCards, value);
    }

    public IReadOnlyList<DesktopExternalAccountViewModel> Accounts
    {
        get => accounts;
        private set => SetProperty(ref accounts, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (!SetProperty(ref isBusy, value))
                return;

            OnPropertyChanged(nameof(CanSaveSoulseekAccount));
            OnPropertyChanged(nameof(CanDisconnectSoulseekAccount));
        }
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
        => await ExecuteAsync(async () =>
        {
            ApplySoulseekAccount(await apiClient.GetSoulseekAccountAsync(cancellationToken));
            var providers = await apiClient.GetProvidersAsync(cancellationToken);
            ProviderCards = providers
                .Select(ProviderConnectionCardViewModel.FromCapability)
                .OrderBy(provider => provider.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            await LoadAccountsAsync(cancellationToken);
            return true;
        });

    public async Task<SoulseekAccountDto?> SaveSoulseekAccountAsync(CancellationToken cancellationToken = default)
    {
        SoulseekAccountDto? configured = null;
        var succeeded = await ExecuteAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(SoulseekUsername) || SoulseekPassword.Length == 0)
            {
                ErrorMessage = "Enter a Soulseek username and password.";
                return false;
            }

            configured = await apiClient.ConfigureSoulseekAccountAsync(
                new ConfigureSoulseekAccountRequestDto(SoulseekUsername, SoulseekPassword),
                cancellationToken);
            ApplySoulseekAccount(configured);
            SoulseekPassword = string.Empty;
            return true;
        });

        return succeeded ? configured : null;
    }

    public async Task<SoulseekAccountDto?> DisconnectSoulseekAccountAsync(CancellationToken cancellationToken = default)
    {
        SoulseekAccountDto? disconnected = null;
        var succeeded = await ExecuteAsync(async () =>
        {
            disconnected = await apiClient.DisconnectSoulseekAccountAsync(cancellationToken);
            ApplySoulseekAccount(disconnected);
            SoulseekPassword = string.Empty;
            return true;
        });

        return succeeded ? disconnected : null;
    }

    public async Task<ExternalAccountDto?> DisconnectAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        ExternalAccountDto? disconnected = null;
        var succeeded = await ExecuteAsync(async () =>
        {
            disconnected = await apiClient.DisconnectExternalAccountAsync(accountId, cancellationToken);
            if (disconnected is null)
            {
                ErrorMessage = "Account was not found.";
                return false;
            }

            await LoadAccountsAsync(cancellationToken);
            return true;
        });

        return succeeded ? disconnected : null;
    }

    private async Task LoadAccountsAsync(CancellationToken cancellationToken)
    {
        var accountDtos = await apiClient.GetExternalAccountsAsync(cancellationToken);
        Accounts = accountDtos
            .Select(account => new DesktopExternalAccountViewModel(account))
            .OrderBy(account => account.ProviderId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(account => account.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void ApplySoulseekAccount(SoulseekAccountDto account)
    {
        SoulseekAccount = account;
        SoulseekUsername = account.Username ?? string.Empty;
    }

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
}

public sealed class DesktopExternalAccountViewModel(ExternalAccountDto account)
{
    public Guid AccountId { get; } = account.AccountId;

    public string ProviderId { get; } = account.ProviderId;

    public string ExternalUserId { get; } = account.ExternalUserId;

    public string DisplayName { get; } = account.DisplayName;

    public string Status { get; } = account.Status;

    public DateTimeOffset? LastAuthorizedAtUtc { get; } = account.LastAuthorizedAtUtc;

    public bool CanDisconnect { get; } = !StringComparer.Ordinal.Equals(account.Status, "Disconnected");

    public string StatusSummary => LastAuthorizedAtUtc is { } authorizedAt
        ? $"{Status} since {authorizedAt:yyyy-MM-dd HH:mm} UTC"
        : Status;
}
