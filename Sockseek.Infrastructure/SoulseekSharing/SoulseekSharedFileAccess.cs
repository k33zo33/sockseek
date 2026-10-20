namespace Sockseek.Infrastructure.SoulseekSharing;

public sealed class SoulseekSharedFileUnavailableException : IOException
{
    public SoulseekSharedFileUnavailableException(string errorCode)
        : base($"The shared file is unavailable ({errorCode}).")
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}

public static class SoulseekSharedFileAccess
{
    public static FileStream OpenRead(SoulseekSharedFile sharedFile, long offset = 0)
    {
        ArgumentNullException.ThrowIfNull(sharedFile);
        if (offset < 0 || offset > sharedFile.Size)
            throw new ArgumentOutOfRangeException(nameof(offset));

        ValidateCurrentPath(sharedFile);

        FileStream stream;
        try
        {
            stream = new FileStream(
                sharedFile.LocalPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
        catch (Exception exception) when (IsFilesystemFailure(exception))
        {
            throw new SoulseekSharedFileUnavailableException("open_failed");
        }

        try
        {
            ValidateCurrentPath(sharedFile);
            if (stream.Length != sharedFile.Size)
                throw new SoulseekSharedFileUnavailableException("file_changed");
            stream.Seek(offset, SeekOrigin.Begin);
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private static void ValidateCurrentPath(SoulseekSharedFile sharedFile)
    {
        string rootPath;
        string filePath;
        try
        {
            rootPath = Path.GetFullPath(sharedFile.RootPath);
            filePath = Path.GetFullPath(sharedFile.LocalPath);
        }
        catch (Exception exception) when (IsFilesystemFailure(exception))
        {
            throw new SoulseekSharedFileUnavailableException("invalid_path");
        }

        if (!IsContained(rootPath, filePath))
            throw new SoulseekSharedFileUnavailableException("outside_root");

        try
        {
            var root = new DirectoryInfo(rootPath);
            if (!root.Exists)
                throw new SoulseekSharedFileUnavailableException("root_missing");
            if (IsReparsePoint(root.Attributes))
                throw new SoulseekSharedFileUnavailableException("reparse_point");

            string relativePath = Path.GetRelativePath(rootPath, filePath);
            string[] segments = relativePath.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);
            string currentDirectory = rootPath;
            for (int index = 0; index < segments.Length - 1; index++)
            {
                currentDirectory = Path.Combine(currentDirectory, segments[index]);
                var directory = new DirectoryInfo(currentDirectory);
                if (!directory.Exists)
                    throw new SoulseekSharedFileUnavailableException("directory_missing");
                if (IsReparsePoint(directory.Attributes))
                    throw new SoulseekSharedFileUnavailableException("reparse_point");
            }

            var file = new FileInfo(filePath);
            if (!file.Exists)
                throw new SoulseekSharedFileUnavailableException("file_missing");
            if (IsReparsePoint(file.Attributes))
                throw new SoulseekSharedFileUnavailableException("reparse_point");
            if (file.Length != sharedFile.Size)
                throw new SoulseekSharedFileUnavailableException("file_changed");
        }
        catch (SoulseekSharedFileUnavailableException)
        {
            throw;
        }
        catch (Exception exception) when (IsFilesystemFailure(exception))
        {
            throw new SoulseekSharedFileUnavailableException("path_unreadable");
        }
    }

    private static bool IsContained(string rootPath, string candidatePath)
    {
        string normalizedRoot = rootPath
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return candidatePath.StartsWith(normalizedRoot, comparison);
    }

    private static bool IsReparsePoint(FileAttributes attributes)
        => (attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;

    private static bool IsFilesystemFailure(Exception exception)
        => exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException;
}
