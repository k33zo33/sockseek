using Sockseek.Infrastructure.LocalLibrary;

namespace Sockseek.Server;

public sealed class LocalLibraryBackgroundScanHostedService : BackgroundService
{
    private static readonly TimeSpan DefaultRootRefreshInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultDebounceInterval = TimeSpan.FromSeconds(2);

    private readonly LocalLibraryEndpointService library;
    private readonly TimeSpan rootRefreshInterval;
    private readonly TimeSpan debounceInterval;
    private readonly Func<LocalLibraryFileWatcher> watcherFactory;
    private readonly SemaphoreSlim scanRequests = new(0);
    private readonly object watcherLock = new();

    private LocalLibraryFileWatcher? watcher;
    private IReadOnlyList<string> watchedRoots = [];

    public LocalLibraryBackgroundScanHostedService(LocalLibraryEndpointService library)
        : this(library, DefaultRootRefreshInterval, DefaultDebounceInterval, () => new LocalLibraryFileWatcher())
    {
    }

    public LocalLibraryBackgroundScanHostedService(
        LocalLibraryEndpointService library,
        TimeSpan rootRefreshInterval,
        TimeSpan debounceInterval)
        : this(library, rootRefreshInterval, debounceInterval, () => new LocalLibraryFileWatcher())
    {
    }

    private LocalLibraryBackgroundScanHostedService(
        LocalLibraryEndpointService library,
        TimeSpan rootRefreshInterval,
        TimeSpan debounceInterval,
        Func<LocalLibraryFileWatcher> watcherFactory)
    {
        this.library = library;
        this.rootRefreshInterval = rootRefreshInterval;
        this.debounceInterval = debounceInterval;
        this.watcherFactory = watcherFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshWatchedRootsAsync(stoppingToken);

                var refreshDelay = Task.Delay(rootRefreshInterval, stoppingToken);
                var fileChange = scanRequests.WaitAsync(stoppingToken);
                var completed = await Task.WhenAny(refreshDelay, fileChange);

                if (completed != fileChange)
                    continue;

                await fileChange;
                await DebouncedScanAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch
            {
                await Task.Delay(rootRefreshInterval, stoppingToken);
            }
        }
    }

    public override void Dispose()
    {
        DisposeWatcher();
        scanRequests.Dispose();
        base.Dispose();
    }

    private async Task RefreshWatchedRootsAsync(CancellationToken cancellationToken)
    {
        var roots = (await library.ListRootsAsync(cancellationToken))
            .Where(root => root.Enabled)
            .Select(root => root.Path)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (roots.SequenceEqual(watchedRoots, StringComparer.OrdinalIgnoreCase))
            return;

        RestartWatcher(roots);
        watchedRoots = roots;

        if (roots.Length > 0)
            RequestScan();
    }

    private async Task DebouncedScanAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(debounceInterval, cancellationToken);
        while (scanRequests.Wait(0))
        {
        }

        await library.ScanAsync(cancellationToken);
    }

    private void RestartWatcher(IReadOnlyList<string> roots)
    {
        lock (watcherLock)
        {
            DisposeWatcher();

            if (roots.Count == 0)
                return;

            watcher = watcherFactory();
            watcher.Changed += OnFileChanged;
            watcher.Start(roots);
        }
    }

    private void DisposeWatcher()
    {
        lock (watcherLock)
        {
            if (watcher == null)
                return;

            watcher.Changed -= OnFileChanged;
            watcher.Dispose();
            watcher = null;
        }
    }

    private void OnFileChanged(object? sender, LocalLibraryFileChange change)
        => RequestScan();

    private void RequestScan()
    {
        try
        {
            scanRequests.Release();
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
