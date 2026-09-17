using Microsoft.EntityFrameworkCore;
using Sockseek.Application.Common;
using Sockseek.Infrastructure.Persistence.Entities;

namespace Sockseek.Infrastructure.Persistence;

public sealed class LibraryRootStore(SockseekDbContext dbContext, IClock clock)
{
    public async Task<Guid> AddOrUpdateAsync(
        string path,
        string? displayName = null,
        bool enabled = true,
        CancellationToken cancellationToken = default)
    {
        string normalizedPath = NormalizeDirectoryPath(path);
        DateTimeOffset now = clock.UtcNow;

        var entity = await dbContext.LibraryRoots.SingleOrDefaultAsync(x => x.Path == normalizedPath, cancellationToken);
        if (entity == null)
        {
            entity = new LibraryRootEntity
            {
                Id = Guid.NewGuid(),
                Path = normalizedPath,
                CreatedAtUtc = now,
            };
            dbContext.LibraryRoots.Add(entity);
        }

        entity.DisplayName = Normalize(displayName);
        entity.Enabled = enabled;
        entity.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<IReadOnlyList<LibraryRootRecord>> ListAsync(CancellationToken cancellationToken = default)
        => await dbContext.LibraryRoots
            .OrderBy(x => x.Path)
            .Select(x => new LibraryRootRecord(
                x.Id,
                x.Path,
                x.DisplayName,
                x.Enabled,
                x.CreatedAtUtc,
                x.UpdatedAtUtc,
                x.LastScanStartedUtc,
                x.LastScanCompletedUtc))
            .ToListAsync(cancellationToken);

    public async Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.LibraryRoots.FindAsync([id], cancellationToken);
        if (entity == null)
            return false;

        dbContext.LibraryRoots.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task MarkScanStartedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.LibraryRoots.FindAsync([id], cancellationToken)
            ?? throw new InvalidOperationException("Library root was not found.");

        entity.LastScanStartedUtc = clock.UtcNow;
        entity.UpdatedAtUtc = entity.LastScanStartedUtc.Value;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkScanCompletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.LibraryRoots.FindAsync([id], cancellationToken)
            ?? throw new InvalidOperationException("Library root was not found.");

        entity.LastScanCompletedUtc = clock.UtcNow;
        entity.UpdatedAtUtc = entity.LastScanCompletedUtc.Value;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeDirectoryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("path is required.", nameof(path));

        string normalized = Path.GetFullPath(path).Trim().Replace('\\', '/').TrimEnd('/');
        return normalized.Length == 0 ? "/" : normalized + "/";
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
