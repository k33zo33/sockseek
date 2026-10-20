using Microsoft.EntityFrameworkCore;
using Sockseek.Application.Common;
using Sockseek.Infrastructure.Persistence.Entities;

namespace Sockseek.Infrastructure.Persistence;

public sealed class SoulseekShareRootStore(SockseekDbContext dbContext, IClock clock)
{
    public const int MaximumVirtualNameLength = 128;

    public async Task<SoulseekShareRootRecord> SaveAsync(
        Guid? id,
        string path,
        string virtualName,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        string normalizedPath = NormalizeDirectoryPath(path);
        string normalizedVirtualName = NormalizeVirtualName(virtualName);
        DateTimeOffset now = clock.UtcNow;

        SoulseekShareRootEntity? entity = id is { } rootId
            ? await dbContext.SoulseekShareRoots.SingleOrDefaultAsync(root => root.Id == rootId, cancellationToken)
                ?? throw new KeyNotFoundException("Soulseek share root was not found.")
            : await dbContext.SoulseekShareRoots.SingleOrDefaultAsync(root => root.Path == normalizedPath, cancellationToken);

        if (entity is null)
        {
            entity = new SoulseekShareRootEntity
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = now,
            };
            dbContext.SoulseekShareRoots.Add(entity);
        }

        entity.Path = normalizedPath;
        entity.VirtualName = normalizedVirtualName;
        entity.Enabled = enabled;
        entity.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToRecord(entity);
    }

    public async Task<IReadOnlyList<SoulseekShareRootRecord>> ListAsync(
        CancellationToken cancellationToken = default)
        => await dbContext.SoulseekShareRoots
            .OrderBy(root => root.VirtualName)
            .Select(root => new SoulseekShareRootRecord(
                root.Id,
                root.Path,
                root.VirtualName,
                root.Enabled,
                root.CreatedAtUtc,
                root.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

    public async Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.SoulseekShareRoots.FindAsync([id], cancellationToken);
        if (entity is null)
            return false;

        dbContext.SoulseekShareRoots.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    internal static string NormalizeDirectoryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Share root path is required.", nameof(path));

        string fullPath = Path.GetFullPath(path.Trim());
        string rootPath = Path.GetPathRoot(fullPath) ?? string.Empty;
        if (PathEquals(fullPath, rootPath))
            return rootPath;

        return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    internal static string NormalizeVirtualName(string virtualName)
    {
        if (string.IsNullOrWhiteSpace(virtualName))
            throw new ArgumentException("Share name is required.", nameof(virtualName));

        string normalized = virtualName.Trim();
        if (normalized.Length > MaximumVirtualNameLength)
            throw new ArgumentException($"Share name cannot exceed {MaximumVirtualNameLength} characters.", nameof(virtualName));
        if (normalized is "." or ".."
            || normalized.Contains('/')
            || normalized.Contains('\\')
            || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("Share name contains invalid path characters.", nameof(virtualName));
        }

        return normalized;
    }

    private static SoulseekShareRootRecord ToRecord(SoulseekShareRootEntity entity)
        => new(
            entity.Id,
            entity.Path,
            entity.VirtualName,
            entity.Enabled,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);

    private static bool PathEquals(string left, string right)
        => string.Equals(
            left.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            right.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
