using System.Windows.Input;
using Sockseek.Api;

namespace Sockseek.Desktop;

public sealed class DesktopAccountsViewModel : ObservableObject
{
    private readonly SockseekApiClient apiClient;
    private IReadOnlyList<ProviderConnectionCardViewModel> providerCards = [];
    private IReadOnlyList<DesktopExternalAccountViewModel> accounts = [];
    private bool isBusy;
    private string? errorMessage;

    public DesktopAccountsViewModel(SockseekApiClient apiClient)
    {
        this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        RefreshCommand = new DesktopAsyncCommand(() => RefreshAsync());
        DisconnectCommand = new DesktopAsyncParameterCommand<Guid>(accountId => DisconnectAsync(accountId));
    }

    public ICommand RefreshCommand { get; }

    public ICommand DisconnectCommand { get; }

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
        private set => SetProperty(ref isBusy, value);
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
        => await ExecuteAsync(async () =>
        {
            var providers = await apiClient.GetProvidersAsync(cancellationToken);
            ProviderCards = providers
                .Select(ProviderConnectionCardViewModel.FromCapability)
                .OrderBy(provider => provider.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            await LoadAccountsAsync(cancellationToken);
            return true;
        });

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
