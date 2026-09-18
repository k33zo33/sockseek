namespace Sockseek.Infrastructure.LocalLibrary;

public sealed class LocalLibraryFileWatcher : IDisposable
{
    private readonly IReadOnlySet<string> supportedExtensions;
    private readonly List<FileSystemWatcher> watchers = [];
    private bool disposed;

    public LocalLibraryFileWatcher(IReadOnlySet<string>? supportedExtensions = null)
        => this.supportedExtensions = supportedExtensions ?? LocalLibraryScanner.DefaultSupportedExtensions;

    public event EventHandler<LocalLibraryFileChange>? Changed;

    public void Start(IReadOnlyList<string> roots)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(roots);

        if (watchers.Count > 0)
            throw new InvalidOperationException("Local library file watcher is already started.");
        if (roots.Count == 0)
            throw new ArgumentException("At least one library root is required.", nameof(roots));

        foreach (string root in NormalizeRoots(roots))
        {
            if (!Directory.Exists(root))
                continue;

            var watcher = new FileSystemWatcher(root)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.Size,
            };

            watcher.Created += OnCreated;
            watcher.Changed += OnChanged;
            watcher.Deleted += OnDeleted;
            watcher.Renamed += OnRenamed;
            watcher.EnableRaisingEvents = true;
            watchers.Add(watcher);
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;

        foreach (var watcher in watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Created -= OnCreated;
            watcher.Changed -= OnChanged;
            watcher.Deleted -= OnDeleted;
            watcher.Renamed -= OnRenamed;
            watcher.Dispose();
        }

        watchers.Clear();
        disposed = true;
    }

    private void OnCreated(object sender, FileSystemEventArgs args)
        => Publish(LocalLibraryFileChangeKind.Created, args.FullPath);

    private void OnChanged(object sender, FileSystemEventArgs args)
        => Publish(LocalLibraryFileChangeKind.Changed, args.FullPath);

    private void OnDeleted(object sender, FileSystemEventArgs args)
        => Publish(LocalLibraryFileChangeKind.Deleted, args.FullPath);

    private void OnRenamed(object sender, RenamedEventArgs args)
    {
        string oldPath = NormalizePath(args.OldFullPath);
        string newPath = NormalizePath(args.FullPath);
        if (!IsSupported(oldPath) && !IsSupported(newPath))
            return;

        Changed?.Invoke(this, new LocalLibraryFileChange(LocalLibraryFileChangeKind.Renamed, newPath, oldPath));
    }

    private void Publish(LocalLibraryFileChangeKind kind, string path)
    {
        string normalizedPath = NormalizePath(path);
        if (IsSupported(normalizedPath))
            Changed?.Invoke(this, new LocalLibraryFileChange(kind, normalizedPath));
    }

    private bool IsSupported(string path)
        => supportedExtensions.Contains(Path.GetExtension(path));

    private static IReadOnlyList<string> NormalizeRoots(IReadOnlyList<string> roots)
        => roots
            .Select(root => string.IsNullOrWhiteSpace(root)
                ? throw new ArgumentException("Library root paths are required.", nameof(roots))
                : Path.GetFullPath(root))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string NormalizePath(string path)
        => Path.GetFullPath(path).Trim().Replace('\\', '/');
}
