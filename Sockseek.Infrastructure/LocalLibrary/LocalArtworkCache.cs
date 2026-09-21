using System.Security.Cryptography;
using TagLib;
using TagLibFile = TagLib.File;

namespace Sockseek.Infrastructure.LocalLibrary;

public sealed class LocalArtworkCache
{
    private readonly string cacheDirectory;

    public LocalArtworkCache(string cacheDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        this.cacheDirectory = Path.GetFullPath(cacheDirectory);
    }

    public async Task<string?> ExtractAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var file = TagLibFile.Create(path);
            var picture = file.Tag.Pictures.FirstOrDefault(candidate => candidate.Data.Count > 0);
            if (picture == null)
                return null;

            var data = picture.Data.Data;
            if (data.Length == 0)
                return null;

            Directory.CreateDirectory(cacheDirectory);
            var extension = ExtensionFor(picture.MimeType);
            var fileName = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant() + extension;
            var destination = Path.Combine(cacheDirectory, fileName);
            if (!System.IO.File.Exists(destination))
                await System.IO.File.WriteAllBytesAsync(destination, data, cancellationToken);

            return destination;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (IsNonFatalArtworkFailure(ex))
        {
            return null;
        }
    }

    private static string ExtensionFor(string? mimeType)
        => mimeType?.Trim().ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".bin",
        };

    private static bool IsNonFatalArtworkFailure(Exception ex)
        => ex is CorruptFileException
            or UnsupportedFormatException
            or IOException
            or UnauthorizedAccessException;
}
