using System.Security.Cryptography;
using System.Text;
using Sockseek.Api;
using Sockseek.Core.Services;

namespace Sockseek.Server;

public sealed class SoulseekUploadService : IDisposable
{
    internal const int MaximumRetainedTerminalUploads = 100;
    internal const int MaximumVisibleUploads = 200;
    private static readonly TimeSpan ProgressInvalidationInterval = TimeSpan.FromMilliseconds(250);

    private readonly EngineSupervisor supervisor;
    private readonly ServerEventBroadcaster broadcaster;
    private readonly Lock gate = new();
    private readonly LinkedList<TerminalUpload> terminalUploads = [];
    private readonly Dictionary<UploadKey, LinkedListNode<TerminalUpload>> terminalUploadsByKey = [];
    private readonly Timer progressTimer;
    private long revision;
    private bool progressPublishScheduled;
    private bool disposed;

    public SoulseekUploadService(EngineSupervisor supervisor, ServerEventBroadcaster broadcaster)
    {
        this.supervisor = supervisor;
        this.broadcaster = broadcaster;
        progressTimer = new Timer(OnProgressTimer, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        supervisor.UploadChanged += OnUploadChanged;
        supervisor.EngineCreated += OnEngineCreated;
        supervisor.SoulseekSessionReady += OnSoulseekSessionReady;
    }

    public SoulseekUploadSnapshotDto GetSnapshot()
    {
        var current = supervisor.GetUploadTransfers();
        TerminalUpload[] retained;
        long currentRevision;
        lock (gate)
        {
            retained = terminalUploads.ToArray();
            currentRevision = revision;
        }

        var currentByKey = current
            .GroupBy(CreateKey)
            .Select(group => group.Last())
            .ToArray();
        var currentKeys = currentByKey.Select(CreateKey).ToHashSet();
        var combined = currentByKey
            .OrderBy(upload => upload.IsTerminal)
            .ThenByDescending(upload => upload.StartedAtUtc)
            .Select(ToDto)
            .Concat(retained
                .Where(upload => !currentKeys.Contains(upload.Key))
                .Select(upload => ToDto(upload.Upload)))
            .ToArray();
        int activeCount = currentByKey.Count(upload => !upload.IsTerminal);
        int terminalCount = combined.Length - activeCount;
        var visible = combined.Take(MaximumVisibleUploads).ToArray();

        return new SoulseekUploadSnapshotDto(
            currentRevision,
            activeCount,
            terminalCount,
            combined.Length - visible.Length,
            visible);
    }

    private void OnUploadChanged(object? sender, SoulseekUploadChangedEventArgs change)
    {
        long nextRevision;
        lock (gate)
        {
            if (disposed)
                return;

            if (change.Upload.IsTerminal)
                RetainTerminalLocked(change.Upload);
            nextRevision = ++revision;

            if (!change.StateChanged)
            {
                if (!progressPublishScheduled)
                {
                    progressPublishScheduled = true;
                    progressTimer.Change(ProgressInvalidationInterval, Timeout.InfiniteTimeSpan);
                }
                return;
            }

            progressPublishScheduled = false;
            progressTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        PublishChanged(nextRevision, "state-changed");
    }

    private void RetainTerminalLocked(SoulseekUploadTransferSnapshot upload)
    {
        var key = CreateKey(upload);
        if (terminalUploadsByKey.Remove(key, out var existing))
            terminalUploads.Remove(existing);

        var node = terminalUploads.AddFirst(new TerminalUpload(key, upload));
        terminalUploadsByKey[key] = node;
        while (terminalUploads.Count > MaximumRetainedTerminalUploads)
        {
            var removed = terminalUploads.Last!;
            terminalUploads.RemoveLast();
            terminalUploadsByKey.Remove(removed.Value.Key);
        }
    }

    private void OnProgressTimer(object? state)
    {
        _ = state;
        long currentRevision;
        lock (gate)
        {
            if (disposed || !progressPublishScheduled)
                return;
            progressPublishScheduled = false;
            currentRevision = revision;
        }

        PublishChanged(currentRevision, "progress-updated");
    }

    private void OnEngineCreated(Core.DownloadEngine engine)
    {
        _ = engine;
        PublishLifecycleChange("engine-created");
    }

    private void OnSoulseekSessionReady(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        PublishLifecycleChange("session-ready");
    }

    private void PublishLifecycleChange(string reason)
    {
        long nextRevision;
        lock (gate)
        {
            if (disposed)
                return;
            nextRevision = ++revision;
        }
        PublishChanged(nextRevision, reason);
    }

    private void PublishChanged(long currentRevision, string reason)
        => broadcaster.PublishGlobal(
            "soulseek.upload-state.changed",
            new SoulseekUploadStateChangedEventDto(currentRevision, reason));

    private static SoulseekUploadDto ToDto(SoulseekUploadTransferSnapshot upload)
        => new(
            CreateLocalId(CreateKey(upload)),
            upload.Username,
            GetDisplayFilename(upload.RemoteFilename),
            upload.State,
            upload.Size,
            upload.BytesTransferred,
            upload.BytesPerSecond,
            upload.StartedAtUtc,
            upload.EndedAtUtc,
            upload.IsTerminal);

    private static string GetDisplayFilename(string remoteFilename)
    {
        string filename = Path.GetFileName(remoteFilename.Replace('\\', '/')).Trim();
        return filename.Length == 0 ? "shared file" : filename;
    }

    private static UploadKey CreateKey(SoulseekUploadTransferSnapshot upload)
        => new(upload.Username, upload.RemoteFilename, upload.Token);

    private static Guid CreateLocalId(UploadKey key)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{key.Username.Length}:{key.Username}{key.RemoteFilename.Length}:{key.RemoteFilename}{key.Token}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
                return;
            disposed = true;
            progressPublishScheduled = false;
        }

        supervisor.UploadChanged -= OnUploadChanged;
        supervisor.EngineCreated -= OnEngineCreated;
        supervisor.SoulseekSessionReady -= OnSoulseekSessionReady;
        progressTimer.Dispose();
    }

    private sealed record UploadKey(string Username, string RemoteFilename, int Token);
    private sealed record TerminalUpload(UploadKey Key, SoulseekUploadTransferSnapshot Upload);
}
