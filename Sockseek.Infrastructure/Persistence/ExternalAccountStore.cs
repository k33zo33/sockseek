using Microsoft.EntityFrameworkCore;

namespace Sockseek.Infrastructure.Persistence;

public sealed class ExternalAccountStore(SockseekDbContext dbContext)
{
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
}
