using Microsoft.EntityFrameworkCore;
using Sockseek.Application.Common;
using Sockseek.Infrastructure.Persistence.Entities;

namespace Sockseek.Infrastructure.Persistence;

public sealed class SoulseekAccountStore(SockseekDbContext dbContext, IClock clock)
{
    internal const int SingletonId = 1;
    public const int MaximumUsernameLength = 128;
    public const int MaximumSecretReferenceLength = 512;

    public async Task<SoulseekAccountRecord?> GetAsync(CancellationToken cancellationToken = default)
        => await dbContext.SoulseekAccounts
            .AsNoTracking()
            .Where(account => account.Id == SingletonId)
            .Select(account => new SoulseekAccountRecord(
                account.Username,
                account.SecretReference,
                account.CreatedAtUtc,
                account.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<SoulseekAccountRecord> SaveAsync(
        string username,
        string secretReference,
        CancellationToken cancellationToken = default)
    {
        string normalizedUsername = NormalizeUsername(username);
        string normalizedReference = NormalizeSecretReference(secretReference);
        DateTimeOffset now = clock.UtcNow;
        var account = await dbContext.SoulseekAccounts
            .SingleOrDefaultAsync(candidate => candidate.Id == SingletonId, cancellationToken);

        if (account is null)
        {
            account = new SoulseekAccountEntity
            {
                Id = SingletonId,
                CreatedAtUtc = now,
            };
            dbContext.SoulseekAccounts.Add(account);
        }

        account.Username = normalizedUsername;
        account.SecretReference = normalizedReference;
        account.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new SoulseekAccountRecord(
            account.Username,
            account.SecretReference,
            account.CreatedAtUtc,
            account.UpdatedAtUtc);
    }

    public async Task<bool> RemoveAsync(CancellationToken cancellationToken = default)
    {
        var account = await dbContext.SoulseekAccounts
            .SingleOrDefaultAsync(candidate => candidate.Id == SingletonId, cancellationToken);
        if (account is null)
            return false;

        dbContext.SoulseekAccounts.Remove(account);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public static string NormalizeUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Soulseek username is required.", nameof(username));

        string normalized = username.Trim();
        if (normalized.Length > MaximumUsernameLength)
            throw new ArgumentException($"Soulseek username cannot exceed {MaximumUsernameLength} characters.", nameof(username));
        if (normalized.Any(char.IsControl))
            throw new ArgumentException("Soulseek username contains invalid control characters.", nameof(username));
        return normalized;
    }

    private static string NormalizeSecretReference(string secretReference)
    {
        if (string.IsNullOrWhiteSpace(secretReference))
            throw new ArgumentException("Soulseek secret reference is required.", nameof(secretReference));

        string normalized = secretReference.Trim();
        if (normalized.Length > MaximumSecretReferenceLength)
            throw new ArgumentException(
                $"Soulseek secret reference cannot exceed {MaximumSecretReferenceLength} characters.",
                nameof(secretReference));
        if (!normalized.StartsWith("secret://", StringComparison.Ordinal))
            throw new ArgumentException("Soulseek secret reference must be opaque.", nameof(secretReference));
        return normalized;
    }
}

public sealed record SoulseekAccountRecord(
    string Username,
    string SecretReference,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
