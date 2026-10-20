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
public sealed class SoulseekChatEndpointTests
{
    [TestMethod]
    public async Task ChatEndpoints_AreAuthenticatedBoundedRedactedAndReconnectSafe()
    {
        using var temp = TemporaryDirectory.Create();
        string outputRoot = Path.Combine(temp.Path, "output");
        Directory.CreateDirectory(outputRoot);
        var soulseekClient = new MockSoulseekClient([]);
        soulseekClient.AvailablePublicChatRooms.Add(new RoomInfo("music", 12));
        soulseekClient.AvailablePrivateChatRooms.Add(new RoomInfo("friends", 3));
        int port = GetFreeTcpPort();
        string url = $"http://127.0.0.1:{port}";
        const string sessionToken = "chat-test-token";
        await using var app = ServerHost.Build([], new ServerOptions
        {
            DatabasePath = Path.Combine(temp.Path, "sockseek.db"),
            DatabaseBackupDir = Path.Combine(temp.Path, "backups"),
            ClientFactory = _ => soulseekClient,
            Engine = new EngineSettings { UseRandomLogin = true },
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
            using var unauthorized = await anonymous.GetAsync("/api/v1/soulseek/chat");
            Assert.AreEqual(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

            using var http = SockseekApiClient.CreateHttpClient(url, sessionToken);
            var client = new SockseekApiClient(http);
            var initial = await client.GetSoulseekChatAsync();
            Assert.AreEqual(0, initial.Messages.Count);
            Assert.AreEqual(0, initial.JoinedRooms.Count);
            Assert.IsFalse(initial.PublicChatEnabled);

            var chatEvents = new ConcurrentQueue<ServerEventEnvelopeDto>();
            var broadcaster = app.Services.GetRequiredService<ServerEventBroadcaster>();
            void CaptureChatEvent(ServerEventEnvelopeDto envelope)
            {
                if (envelope.Type == "soulseek.chat-state.changed")
                    chatEvents.Enqueue(envelope);
            }

            broadcaster.EventPublished += CaptureChatEvent;
            var refreshed = await client.RefreshSoulseekChatRoomsAsync();
            Assert.AreEqual(2, refreshed.AvailableRooms.Count);
            Assert.AreEqual("music", refreshed.AvailableRooms[0].Name);

            var joined = await client.JoinSoulseekChatRoomAsync(new JoinSoulseekChatRoomRequestDto("music"));
            Assert.AreEqual("music", joined.JoinedRooms.Single().Name);
            Assert.IsTrue(soulseekClient.JoinedChatRooms.Contains("music"));

            var privateSent = await client.SendSoulseekPrivateChatMessageAsync(
                new SendSoulseekPrivateChatMessageRequestDto("listener", "private outgoing"));
            Assert.AreEqual("private outgoing", privateSent.Messages.Last().Message);
            CollectionAssert.AreEqual(
                new[] { ("listener", "private outgoing") },
                soulseekClient.SentPrivateMessages);

            var roomSent = await client.SendSoulseekRoomChatMessageAsync(
                new SendSoulseekRoomChatMessageRequestDto("music", "room outgoing"));
            Assert.AreEqual("room outgoing", roomSent.Messages.Last().Message);
            CollectionAssert.AreEqual(
                new[] { ("music", "room outgoing") },
                soulseekClient.SentRoomMessages);

            var publicStarted = await client.SetSoulseekPublicChatEnabledAsync(true);
            Assert.IsTrue(publicStarted.PublicChatEnabled);
            Assert.IsTrue(soulseekClient.IsPublicChatEnabled);

            soulseekClient.RaisePrivateMessage(
                42,
                new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc),
                "private-sender",
                "private incoming",
                replayed: true);
            soulseekClient.RaisePrivateMessage(
                42,
                new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc),
                "private-sender",
                "duplicate replay",
                replayed: true);
            soulseekClient.RaiseRoomMessage("music", "room-sender", "room incoming");
            soulseekClient.RaisePublicChatMessage("ambient", "public-sender", "public incoming");
            await WaitForAsync(
                () => soulseekClient.AcknowledgedPrivateMessageIds.Contains(42),
                TimeSpan.FromSeconds(5));

            var received = await client.GetSoulseekChatAsync();
            Assert.AreEqual(5, received.Messages.Count);
            Assert.AreEqual(1, received.Messages.Count(message => message.Message == "private incoming"));
            Assert.IsFalse(received.Messages.Any(message => message.Message == "duplicate replay"));
            Assert.AreEqual("public", received.Messages.Last().Kind);
            Assert.AreEqual("ambient", received.Messages.Last().RoomName);
            string snapshotJson = JsonSerializer.Serialize(received);
            Assert.IsFalse(snapshotJson.Contains("protocol", StringComparison.OrdinalIgnoreCase));

            Assert.IsTrue(chatEvents.TryPeek(out var chatEvent));
            Assert.AreEqual(ServerEventCatalog.StateCategory, chatEvent.Category);
            Assert.IsTrue(chatEvent.SnapshotInvalidation);
            Assert.IsInstanceOfType<SoulseekChatStateChangedEventDto>(chatEvent.Payload);
            string eventJson = JsonSerializer.Serialize(chatEvent.Payload);
            Assert.IsFalse(eventJson.Contains("private-sender", StringComparison.Ordinal));
            Assert.IsFalse(eventJson.Contains("private incoming", StringComparison.Ordinal));

            for (int index = 0; index < SoulseekChatService.MaximumRetainedMessages + 5; index++)
                soulseekClient.RaiseRoomMessage("music", "bounded-sender", $"bounded-{index}");

            var bounded = await client.GetSoulseekChatAsync();
            Assert.AreEqual(SoulseekChatService.MaximumRetainedMessages, bounded.Messages.Count);
            Assert.AreEqual("bounded-5", bounded.Messages[0].Message);
            Assert.AreEqual("bounded-504", bounded.Messages[^1].Message);

            var invalid = await Assert.ThrowsExceptionAsync<SockseekApiRequestException>(() =>
                client.SendSoulseekRoomChatMessageAsync(
                    new SendSoulseekRoomChatMessageRequestDto("not-joined", "message")));
            StringAssert.Contains(invalid.Message, "Join the room");

            soulseekClient.ChatException = new TimeoutException("private transport detail");
            var unavailable = await Assert.ThrowsExceptionAsync<SockseekApiRequestException>(() =>
                client.RefreshSoulseekChatRoomsAsync());
            StringAssert.Contains(unavailable.Message, "Soulseek is not connected");
            Assert.IsFalse(unavailable.Message.Contains("private transport detail", StringComparison.Ordinal));
            soulseekClient.ChatException = null;

            soulseekClient.JoinedChatRooms.Clear();
            await soulseekClient.StopPublicChatAsync();
            soulseekClient.RaiseKickedFromServer();
            await WaitForAsync(
                () => soulseekClient.JoinedChatRooms.Contains("music") && soulseekClient.IsPublicChatEnabled,
                TimeSpan.FromSeconds(10));

            var left = await client.LeaveSoulseekChatRoomAsync(new LeaveSoulseekChatRoomRequestDto("music"));
            Assert.AreEqual(0, left.JoinedRooms.Count);
            Assert.IsFalse(soulseekClient.JoinedChatRooms.Contains("music"));
            var publicStopped = await client.SetSoulseekPublicChatEnabledAsync(false);
            Assert.IsFalse(publicStopped.PublicChatEnabled);

            for (int index = 0; index < SoulseekChatService.MaximumJoinedRooms; index++)
            {
                await client.JoinSoulseekChatRoomAsync(
                    new JoinSoulseekChatRoomRequestDto($"bounded-room-{index}"));
            }

            var roomLimit = await Assert.ThrowsExceptionAsync<SockseekApiRequestException>(() =>
                client.JoinSoulseekChatRoomAsync(new JoinSoulseekChatRoomRequestDto("one-room-too-many")));
            StringAssert.Contains(roomLimit.Message, $"At most {SoulseekChatService.MaximumJoinedRooms}");
            broadcaster.EventPublished -= CaptureChatEvent;
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static async Task WaitForAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var startedAt = DateTimeOffset.UtcNow;
        while (!predicate())
        {
            if (DateTimeOffset.UtcNow - startedAt > timeout)
                throw new TimeoutException($"Condition was not satisfied within {timeout}.");
            await Task.Delay(50);
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
                "sockseek-chat-api-" + Guid.NewGuid().ToString("N"));
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
