using Microsoft.EntityFrameworkCore;
using Sockseek.Application.Security;
using Sockseek.Domain.Accounts;
using Sockseek.Infrastructure.Persistence.Entities;
using Sockseek.Integrations.Abstractions;

namespace Sockseek.Infrastructure.Persistence;

public sealed class ExternalAccountStore(SockseekDbContext dbContext)
{
    public async Task<Guid> UpsertAuthorizedAsync(
        ExternalProvider provider,
        ExternalAccountSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var account = await dbContext.ExternalAccounts
            .SingleOrDefaultAsync(entity => entity.Provider == (int)provider
                && entity.ExternalUserId == snapshot.ExternalUserId, cancellationToken);
        if (account == null)
        {
            account = new ExternalAccountEntity
            {
                Id = snapshot.AccountId.Value,
                Provider = (int)provider,
                ExternalUserId = snapshot.ExternalUserId,
            };
            dbContext.ExternalAccounts.Add(account);
        }

        account.DisplayName = snapshot.DisplayName;
        account.SecretReference = snapshot.SecretReference;
        account.Status = (int)ExternalAccountStatus.Authorized;
        account.LastAuthorizedAtUtc = snapshot.AuthorizedAtUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
        return account.Id;
    }

    public async Task<bool> DeleteAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await dbContext.ExternalAccounts
            .SingleOrDefaultAsync(entity => entity.Id == accountId, cancellationToken);
        if (account == null)
            return false;

        var linkedPlaylists = await dbContext.ExternalPlaylists
            .Where(entity => entity.AccountId == accountId)
            .ToListAsync(cancellationToken);

        foreach (var playlist in linkedPlaylists)
            playlist.AccountId = null;

        dbContext.ExternalAccounts.Remove(account);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DisconnectAsync(
        Guid accountId,
        ISecretStore secretStore,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(secretStore);

        var account = await dbContext.ExternalAccounts
            .SingleOrDefaultAsync(entity => entity.Id == accountId, cancellationToken);
        if (account == null)
            return false;

        var secretReference = account.SecretReference;
        if (!string.IsNullOrWhiteSpace(secretReference))
            await secretStore.DeleteAsync(secretReference, cancellationToken);

        account.SecretReference = string.Empty;
        account.Status = (int)ExternalAccountStatus.Disconnected;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> MarkAuthorizationExpiredAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var account = await dbContext.ExternalAccounts
            .SingleOrDefaultAsync(entity => entity.Id == accountId, cancellationToken);
        if (account == null)
            return false;

        account.Status = (int)ExternalAccountStatus.AuthorizationExpired;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
