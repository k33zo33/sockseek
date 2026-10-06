using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Sockseek.Packager;

public static class ReleaseArchiveBuilder
{
    public static ReleaseArchiveResult ArchiveWindows(ReleaseArchiveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<string>();
        var stagingDirectory = Path.GetFullPath(request.StagingDirectory);
        var archivePath = Path.GetFullPath(request.ArchivePath);
        var manifestPath = Path.GetFullPath(request.ManifestPath);

        var validation = ReleaseArtifactValidator.Validate(
            stagingDirectory,
            request.DesktopExecutableName,
            Path.Combine("daemon", request.DaemonExecutableName));
        errors.AddRange(validation.Errors);

        if (IsUnderDirectory(stagingDirectory, archivePath))
            errors.Add("Archive path must not be inside the staging directory.");
        if (IsUnderDirectory(stagingDirectory, manifestPath))
            errors.Add("SHA256 manifest path must not be inside the staging directory.");
        if (string.Equals(archivePath, manifestPath, StringComparisonForCurrentPlatform()))
            errors.Add("Archive path and SHA256 manifest path must be different.");

        if (errors.Count > 0)
            return new ReleaseArchiveResult(archivePath, manifestPath, errors);

        Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        File.Delete(archivePath);
        File.Delete(manifestPath);

        var fileHashes = new List<ReleaseArchiveFileHash>();
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            foreach (var file in Directory.EnumerateFiles(stagingDirectory, "*", SearchOption.AllDirectories)
                         .OrderBy(path => ToArchivePath(stagingDirectory, path), StringComparer.Ordinal))
            {
                var entryName = ToArchivePath(stagingDirectory, file);
                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                entry.LastWriteTime = File.GetLastWriteTimeUtc(file);

                using (var entryStream = entry.Open())
                using (var input = File.OpenRead(file))
                    input.CopyTo(entryStream);

                fileHashes.Add(new ReleaseArchiveFileHash(entryName, ComputeSha256(file)));
            }
        }

        var archiveHash = ComputeSha256(archivePath);
        var manifest = new StringBuilder();
        manifest.AppendLine("# Sockseek Windows release SHA256 manifest");
        manifest.AppendLine($"{archiveHash}  {Path.GetFileName(archivePath)}");
        foreach (var file in fileHashes)
            manifest.AppendLine($"{file.Sha256}  {file.RelativePath}");
        File.WriteAllText(manifestPath, manifest.ToString(), Encoding.UTF8);

        return new ReleaseArchiveResult(archivePath, manifestPath, []);
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string ToArchivePath(string stagingDirectory, string path)
        => Path.GetRelativePath(stagingDirectory, path).Replace(Path.DirectorySeparatorChar, '/');

    private static bool IsUnderDirectory(string directory, string path)
    {
        var directoryPrefix = Path.GetFullPath(directory);
        if (!directoryPrefix.EndsWith(Path.DirectorySeparatorChar))
            directoryPrefix += Path.DirectorySeparatorChar;

        return Path.GetFullPath(path).StartsWith(directoryPrefix, StringComparisonForCurrentPlatform());
    }

    private static StringComparison StringComparisonForCurrentPlatform()
        => OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
}

public sealed record ReleaseArchiveRequest(
    string StagingDirectory,
    string ArchivePath,
    string ManifestPath,
    string DesktopExecutableName,
    string DaemonExecutableName);

public sealed record ReleaseArchiveResult(
    string ArchivePath,
    string ManifestPath,
    IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed record ReleaseArchiveFileHash(
    string RelativePath,
    string Sha256);
