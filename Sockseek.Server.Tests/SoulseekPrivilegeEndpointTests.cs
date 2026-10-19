using System.Net;
using System.Net.Sockets;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;
using Sockseek.Core.Services;
using Sockseek.Core.Settings;
using Sockseek.Server;

namespace Tests.Server;

[TestClass]
public sealed class SoulseekPrivilegeEndpointTests
{
    [TestMethod]
    public async Task PrivilegeEndpoints_RequireAuthBoundNotificationsAndAcknowledgeExplicitly()
    {
        using var temp = TemporaryDirectory.Create();
        string outputRoot = Path.Combine(temp.Path, "output");
        Directory.CreateDirectory(outputRoot);
        var soulseekClient = new LocalFilesSoulseekClient([]) { PrivilegeDays = 21 };
        int port = GetFreeTcpPort();
        string url = $"http://127.0.0.1:{port}";
        const string sessionToken = "privilege-test-token";
        await using var app = ServerHost.Build([], new ServerOptions
        {
            DatabasePath = Path.Combine(temp.Path, "sockseek.db"),
            DatabaseBackupDir = Path.Combine(temp.Path, "backups"),
            ClientFactory = _ => soulseekClient,
            DefaultDownload = new DownloadSettings
            {
                Output =
                {
                    ParentDir = outputRoot,
                },
            },
            Profiles = ProfileCatalog.Empty,
            SessionToken = sessionToken,
        }, url);

        try
        {
            await app.StartAsync();

            using var anonymous = new HttpClient { BaseAddress = new Uri(url) };
            using var unauthorized = await anonymous.GetAsync("/api/v1/soulseek/privileges");
            Assert.AreEqual(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);
            var initial = await client.GetSoulseekPrivilegeStatusAsync();
            Assert.AreEqual(21, initial.RemainingDays);
            Assert.AreEqual(0, initial.Notifications.Count);

            var eventCompletion = new TaskCompletionSource<ServerEventEnvelopeDto>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var broadcaster = app.Services.GetRequiredService<ServerEventBroadcaster>();
            void CapturePrivilegeEvent(ServerEventEnvelopeDto envelope)
            {
                if (envelope.Type == "soulseek.privilege-state.changed")
                    eventCompletion.TrySetResult(envelope);
            }

            broadcaster.EventPublished += CapturePrivilegeEvent;
            soulseekClient.RaisePrivilegeNotification("supporter", 42);
            var privilegeEvent = await eventCompletion.Task.WaitAsync(TimeSpan.FromSeconds(5));
            broadcaster.EventPublished -= CapturePrivilegeEvent;

            Assert.AreEqual(ServerEventCatalog.StateCategory, privilegeEvent.Category);
            Assert.IsTrue(privilegeEvent.SnapshotInvalidation);
            var changed = (SoulseekPrivilegeStateChangedEventDto)privilegeEvent.Payload;
            Assert.IsTrue(changed.RequiresAcknowledgement);

            soulseekClient.RaisePrivilegeNotification("supporter-replay", 42);
            var pending = await client.GetSoulseekPrivilegeStatusAsync();
            var notification = pending.Notifications.Single();
            Assert.AreEqual("supporter", notification.Username);
            Assert.IsTrue(notification.RequiresAcknowledgement);
            Assert.AreEqual(changed.NotificationId, notification.Id);

            soulseekClient.PrivilegeException = new TimeoutException("private transport detail");
            var unavailableStatus = await Assert.ThrowsExceptionAsync<SockseekApiRequestException>(() =>
                client.GetSoulseekPrivilegeStatusAsync());
            StringAssert.Contains(unavailableStatus.Message, "Soulseek is not connected");
            var unavailableAck = await Assert.ThrowsExceptionAsync<SockseekApiRequestException>(() =>
                client.AcknowledgeSoulseekPrivilegeNotificationAsync(notification.Id));
            StringAssert.Contains(unavailableAck.Message, "Soulseek is not connected");
            soulseekClient.PrivilegeException = null;
            Assert.AreEqual(1, (await client.GetSoulseekPrivilegeStatusAsync()).Notifications.Count);

            Assert.IsTrue(await client.AcknowledgeSoulseekPrivilegeNotificationAsync(notification.Id));
            CollectionAssert.AreEqual(new[] { 42 }, soulseekClient.AcknowledgedPrivilegeNotificationIds.ToArray());
            Assert.AreEqual(0, (await client.GetSoulseekPrivilegeStatusAsync()).Notifications.Count);
            Assert.IsFalse(await client.AcknowledgeSoulseekPrivilegeNotificationAsync(notification.Id));

            soulseekClient.RaisePrivilegeNotification("informational");
            var informational = (await client.GetSoulseekPrivilegeStatusAsync()).Notifications.Single();
            Assert.IsFalse(informational.RequiresAcknowledgement);
            await Assert.ThrowsExceptionAsync<SockseekApiRequestException>(() =>
                client.AcknowledgeSoulseekPrivilegeNotificationAsync(informational.Id));

            for (int index = 0; index < SoulseekPrivilegeService.MaximumRetainedNotifications + 5; index++)
                soulseekClient.RaisePrivilegeNotification($"supporter-{index}", 1000 + index);

            var bounded = await client.GetSoulseekPrivilegeStatusAsync();
            Assert.AreEqual(SoulseekPrivilegeService.MaximumRetainedNotifications, bounded.Notifications.Count);
            Assert.AreEqual("supporter-104", bounded.Notifications[0].Username);
            Assert.IsFalse(bounded.Notifications.Any(item => item.Username == "informational"));
        }
        finally
        {
            await app.StopAsync();
        }
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
                "sockseek-privilege-api-" + Guid.NewGuid().ToString("N"));
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
