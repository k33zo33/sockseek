using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Sockseek.Application.Common;
using Sockseek.Domain.Tracks;
using Sockseek.Infrastructure.Persistence;

namespace Sockseek.Infrastructure.LocalLibrary;

public sealed class LocalMediaFileContentHasher(SockseekDbContext dbContext, IClock clock)
{
    public const string Sha256Algorithm = "SHA256";

    public async Task<LocalMediaFileContentHashResult> ComputeMissingHashesAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 500.");

        var files = await dbContext.LocalMediaFiles
            .Where(file => file.Availability == (int)LocalMediaAvailability.Available
                && file.ContentHash == null)
            .OrderBy(file => file.Path)
            .Take(limit)
            .ToListAsync(cancellationToken);

        int hashedFiles = 0;
        int missingFiles = 0;
        int failedFiles = 0;
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (!File.Exists(file.Path))
                {
                    file.Availability = (int)LocalMediaAvailability.Missing;
                    missingFiles++;
                    continue;
                }

                file.ContentHash = await ComputeSha256Async(file.Path, cancellationToken);
                file.ContentHashAlgorithm = Sha256Algorithm;
                file.ContentHashComputedAtUtc = clock.UtcNow;
                hashedFiles++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                failedFiles++;
            }
        }

        if (files.Count > 0)
            await dbContext.SaveChangesAsync(cancellationToken);

        return new LocalMediaFileContentHashResult(files.Count, hashedFiles, missingFiles, failedFiles);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
