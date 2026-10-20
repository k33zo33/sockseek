using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;
using Sockseek.Core.Settings;
using Sockseek.Server;
using Soulseek;
using Tests.ClientTests;
using Directory = System.IO.Directory;

namespace Tests.Server;

[TestClass]
public sealed class SoulseekUploadEndpointTests
{
    [TestMethod]
    public async Task UploadEndpoint_IsAuthenticatedBoundedRedactedAndProgressCoalesced()
    {
        using var temp = TemporaryDirectory.Create();
        string outputRoot = Path.Combine(temp.Path, "output");
        Directory.CreateDirectory(outputRoot);
        var soulseekClient = new MockSoulseekClient([]);
        int port = GetFreeTcpPort();
        string url = $"http://127.0.0.1:{port}";
        const string sessionToken = "upload-test-token";
        await using var app = ServerHost.Build([], new ServerOptions
        {
            DatabasePath = Path.Combine(temp.Path, "sockseek.db"),
            DatabaseBackupDir = Path.Combine(temp.Path, "backups"),
            ClientFactory = _ => soulseekClient,
            Engine = new EngineSettings { UseRandomLogin = true },
            DefaultDownload = new DownloadSettings
            {
                Output = { ParentDir = outputRoot },
            },
            Profiles = ProfileCatalog.Empty,
            SessionToken = sessionToken,
        }, url);

        try
        {
            await app.StartAsync();

            using var anonymous = new HttpClient { BaseAddress = new Uri(url) };
            using var unauthorized = await anonymous.GetAsync("/api/v1/soulseek/uploads");
            Assert.AreEqual(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);
            var initial = await client.GetSoulseekUploadsAsync();
            Assert.AreEqual(0, initial.ActiveCount);
            Assert.AreEqual(0, initial.Uploads.Count);

            var uploadEvents = new ConcurrentQueue<ServerEventEnvelopeDto>();
            var broadcaster = app.Services.GetRequiredService<ServerEventBroadcaster>();
            void CaptureUploadEvent(ServerEventEnvelopeDto envelope)
            {
                if (envelope.Type == "soulseek.upload-state.changed")
                    uploadEvents.Enqueue(envelope);
            }

            broadcaster.EventPublished += CaptureUploadEvent;
            var active = CreateUpload(
                "listener",
                "C:\\private\\share\\track.flac",
                42,
                TransferStates.InProgress,
                bytesTransferred: 25,
                exception: new InvalidOperationException("private transport detail"));
            soulseekClient.SetUploads(active);
            soulseekClient.RaiseTransferStateChanged(TransferStates.Initializing, active);
            await WaitForAsync(() => uploadEvents.Count > 0, TimeSpan.FromSeconds(5));

            var activeSnapshot = await client.GetSoulseekUploadsAsync();
            var visible = activeSnapshot.Uploads.Single();
            Assert.AreEqual(1, activeSnapshot.ActiveCount);
            Assert.AreEqual("listener", visible.Username);
            Assert.AreEqual("track.flac", visible.Filename);
            Assert.AreEqual(25, visible.BytesTransferred);
            Assert.IsFalse(visible.IsTerminal);
            Assert.AreEqual(visible.Id, (await client.GetSoulseekUploadsAsync()).Uploads.Single().Id);
            string snapshotJson = JsonSerializer.Serialize(activeSnapshot);
            Assert.IsFalse(snapshotJson.Contains("C:\\private", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(snapshotJson.Contains("transport detail", StringComparison.Ordinal));

            Assert.IsTrue(uploadEvents.TryDequeue(out var stateEvent));
            Assert.AreEqual(ServerEventCatalog.StateCategory, stateEvent.Category);
            Assert.IsTrue(stateEvent.SnapshotInvalidation);
            Assert.IsInstanceOfType<SoulseekUploadStateChangedEventDto>(stateEvent.Payload);
            string eventJson = JsonSerializer.Serialize(stateEvent.Payload);
            Assert.IsFalse(eventJson.Contains("listener", StringComparison.Ordinal));
            Assert.IsFalse(eventJson.Contains("track.flac", StringComparison.Ordinal));

            while (uploadEvents.TryDequeue(out _))
            {
            }
            for (int index = 0; index < 20; index++)
                soulseekClient.RaiseTransferProgressUpdated(index, active);
            await WaitForAsync(
                () => uploadEvents.Any(envelope =>
                    envelope.Payload is SoulseekUploadStateChangedEventDto changed
                    && changed.Reason == "progress-updated"),
                TimeSpan.FromSeconds(5));
            await Task.Delay(350);
            Assert.AreEqual(
                1,
                uploadEvents.Count(envelope =>
                    envelope.Payload is SoulseekUploadStateChangedEventDto changed
                    && changed.Reason == "progress-updated"));

            while (uploadEvents.TryDequeue(out _))
            {
            }
            int connectCountBeforeReconnect = soulseekClient.ConnectCallCount;
            soulseekClient.RaiseKickedFromServer();
            await WaitForAsync(
                () => soulseekClient.ConnectCallCount > connectCountBeforeReconnect,
                TimeSpan.FromSeconds(5));
            await WaitForAsync(
                () => uploadEvents.Any(envelope =>
                    envelope.Payload is SoulseekUploadStateChangedEventDto changed
                    && changed.Reason == "session-ready"),
                TimeSpan.FromSeconds(5));

            var failed = CreateUpload(
                "listener",
                "C:\\private\\share\\failed.flac",
                99,
                TransferStates.Completed | TransferStates.Errored,
                bytesTransferred: 30,
                exception: new IOException("private failure C:\\private\\share\\failed.flac"));
            soulseekClient.SetUploads();
            soulseekClient.RaiseTransferStateChanged(TransferStates.InProgress, failed);
            var failedSnapshot = await client.GetSoulseekUploadsAsync();
            var failedRow = failedSnapshot.Uploads.Single(upload => upload.Filename == "failed.flac");
            Assert.IsTrue(failedRow.IsTerminal);
            StringAssert.Contains(failedRow.State, "Errored");
            string failedJson = JsonSerializer.Serialize(failedSnapshot);
            Assert.IsFalse(failedJson.Contains("private failure", StringComparison.Ordinal));
            Assert.IsFalse(failedJson.Contains("C:\\private", StringComparison.OrdinalIgnoreCase));

            for (int index = 0; index < SoulseekUploadService.MaximumRetainedTerminalUploads + 5; index++)
            {
                var terminal = CreateUpload(
                    "listener",
                    $"@@Music\\Album\\track-{index}.flac",
                    1000 + index,
                    TransferStates.Completed | TransferStates.Succeeded,
                    bytesTransferred: 100);
                soulseekClient.RaiseTransferStateChanged(TransferStates.InProgress, terminal);
            }

            var bounded = await client.GetSoulseekUploadsAsync();
            Assert.AreEqual(0, bounded.ActiveCount);
            Assert.AreEqual(SoulseekUploadService.MaximumRetainedTerminalUploads, bounded.TerminalCount);
            Assert.AreEqual(SoulseekUploadService.MaximumRetainedTerminalUploads, bounded.Uploads.Count);
            Assert.AreEqual(0, bounded.TruncatedCount);
            Assert.AreEqual("track-104.flac", bounded.Uploads[0].Filename);
            Assert.AreEqual("track-5.flac", bounded.Uploads[^1].Filename);
            broadcaster.EventPublished -= CaptureUploadEvent;
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static Transfer CreateUpload(
        string username,
        string filename,
        int token,
        TransferStates state,
        long bytesTransferred,
        Exception? exception = null)
        => new(
            TransferDirection.Upload,
            username,
            filename,
            token,
            state,
            100,
            0,
            bytesTransferred,
            20,
            DateTime.UtcNow.AddSeconds(-5),
            state.HasFlag(TransferStates.Completed) ? DateTime.UtcNow : null,
            exception: exception);

    private static async Task WaitForAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!predicate() && DateTime.UtcNow < deadline)
            await Task.Delay(20);
        Assert.IsTrue(predicate(), "Expected upload state was not reached before the timeout.");
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path) => Path = path;

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "sockseek-upload-api-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (!Directory.Exists(Path))
                return;

            for (int attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    Directory.Delete(Path, recursive: true);
                    return;
                }
                catch (IOException) when (attempt < 19)
                {
                    Thread.Sleep(100);
                    SqliteConnection.ClearAllPools();
                }
                catch (UnauthorizedAccessException) when (attempt < 19)
                {
                    Thread.Sleep(100);
                    SqliteConnection.ClearAllPools();
                }
            }
        }
    }
}
