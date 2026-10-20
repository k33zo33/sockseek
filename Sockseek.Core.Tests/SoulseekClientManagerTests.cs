using Microsoft.VisualStudio.TestTools.UnitTesting;
using Soulseek;
using Sockseek.Core.Services;
using Sockseek.Core.Settings;
using Tests.ClientTests;

namespace Tests.Core;

[TestClass]
public class SoulseekClientManagerTests
{
    [TestMethod]
    public void CreateRandomStartingToken_ReturnsPositiveToken()
    {
        for (int i = 0; i < 20; i++)
        {
            int token = SoulseekClientManager.CreateRandomStartingToken();

            Assert.IsTrue(token > 0);
            Assert.IsTrue(token < int.MaxValue);
        }
    }

    [TestMethod]
    public void CreateClientOptions_UsesProvidedStartingToken_WithListener()
    {
        const int token = 123_456_789;
        var options = SoulseekClientManager.CreateClientOptions(
            new EngineSettings { ListenPort = 49998 },
            token);

        Assert.AreEqual(token, options.StartingToken);
        Assert.IsFalse(options.AutoAcknowledgePrivateMessages);
        Assert.IsFalse(options.AutoAcknowledgePrivilegeNotifications);
    }

    [TestMethod]
    public void CreateClientOptions_UsesProvidedStartingToken_WithoutListener()
    {
        const int token = 987_654_321;
        var options = SoulseekClientManager.CreateClientOptions(
            new EngineSettings { ListenPort = null },
            token);

        Assert.AreEqual(token, options.StartingToken);
        Assert.IsFalse(options.AutoAcknowledgePrivateMessages);
        Assert.IsFalse(options.AutoAcknowledgePrivilegeNotifications);
    }

    [TestMethod]
    public async Task CreateClientOptions_WithShareSource_MapsSearchBrowseDirectoryAndUploadResolvers()
    {
        var source = new FakeShareSource(new SoulseekShareSourceSnapshot(
            4,
            3,
            [
                new SoulseekShareSourceFile("@@Music\\Artist\\Album\\track.mp3", 5, ".mp3"),
                new SoulseekShareSourceFile("@@Music\\Artist\\Live\\concert.flac", 7, ".flac"),
            ]));
        var mockClient = new MockSoulseekClient(new());
        var options = SoulseekClientManager.CreateClientOptions(
            new EngineSettings { ListenPort = 49998, UserDescription = "Local shares" },
            123,
            source,
            () => "local-user",
            () => mockClient);
        var endpoint = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 1234);

        Assert.AreEqual(1, options.MaximumConcurrentUploads);
        Assert.IsNotNull(options.SearchResponseResolver);
        Assert.IsNotNull(options.BrowseResponseResolver);
        Assert.IsNotNull(options.DirectoryContentsResolver);
        Assert.IsNotNull(options.EnqueueDownload);
        Assert.IsNotNull(options.PlaceInQueueResolver);

        var response = await options.SearchResponseResolver(
            "requester",
            77,
            new SearchQuery(["artist"], ["live"]));
        Assert.AreEqual("local-user", response.Username);
        Assert.AreEqual(77, response.Token);
        Assert.AreEqual(1, response.FileCount);
        Assert.AreEqual("@@Music\\Artist\\Album\\track.mp3", response.Files.Single().Filename);

        var browse = await options.BrowseResponseResolver("requester", endpoint);
        Assert.AreEqual(2, browse.Directories.Count);
        Assert.IsTrue(browse.Directories.All(directory => directory.Name.StartsWith("@@Music", StringComparison.Ordinal)));
        Assert.IsFalse(browse.Directories.SelectMany(directory => directory.Files)
            .Any(file => file.Filename.Contains(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase)));

        var directoryContents = (await options.DirectoryContentsResolver(
            "requester",
            endpoint,
            88,
            "@@Music/Artist/Album")).ToArray();
        Assert.AreEqual(1, directoryContents.Length);
        Assert.AreEqual("@@Music\\Artist\\Album", directoryContents[0].Name);

        Assert.AreEqual(0, await options.PlaceInQueueResolver(
            "requester",
            endpoint,
            "@@Music/Artist/Album/track.mp3"));
        Assert.IsNull(await options.PlaceInQueueResolver(
            "requester",
            endpoint,
            "@@Music\\Artist\\Album\\missing.mp3"));

        await options.EnqueueDownload(
            "requester",
            endpoint,
            "@@Music/Artist/Album/track.mp3");
        var upload = mockClient.EnqueuedUploads.Single();
        Assert.AreEqual("requester", upload.Username);
        Assert.AreEqual("@@Music\\Artist\\Album\\track.mp3", upload.RemoteFilename);
        Assert.AreEqual(5, upload.Size);
    }

    [TestMethod]
    public async Task Login_AdvertisesLegacyCountsInDirectoryThenFileOrder()
    {
        var settings = new EngineSettings
        {
            Username = "user",
            Password = "pass",
            SharedFolders = 3,
            SharedFiles = 7,
        };
        var mockClient = new MockSoulseekClient(new(), initialState: SoulseekClientStates.None);
        using var manager = new SoulseekClientManager(settings, mockClient);

        await manager.EnsureConnectedAndLoggedInAsync(settings);

        Assert.AreEqual((3, 7), mockClient.SharedCountUpdates.Single());
    }

    [TestMethod]
    public async Task LoginAndSnapshotChange_AdvertiseOnlyAuthoritativeSnapshotCounts()
    {
        var settings = new EngineSettings
        {
            Username = "user",
            Password = "pass",
            SharedFolders = 300,
            SharedFiles = 700,
        };
        var source = new FakeShareSource(new SoulseekShareSourceSnapshot(
            1,
            2,
            [new SoulseekShareSourceFile("@@Music\\track.mp3", 5, ".mp3")]));
        var mockClient = new MockSoulseekClient(new(), initialState: SoulseekClientStates.None);
        using var manager = new SoulseekClientManager(settings, mockClient, source);

        await manager.EnsureConnectedAndLoggedInAsync(settings);
        Assert.AreEqual((2, 1), mockClient.SharedCountUpdates.Single());

        source.SetSnapshot(new SoulseekShareSourceSnapshot(
            2,
            4,
            [
                new SoulseekShareSourceFile("@@Music\\one.mp3", 5, ".mp3"),
                new SoulseekShareSourceFile("@@Music\\two.mp3", 5, ".mp3"),
            ]));
        await WaitForConditionAsync(() => mockClient.SharedCountUpdates.Count == 2, TimeSpan.FromSeconds(5));

        CollectionAssert.AreEqual(
            new[] { (2, 1), (4, 2) },
            mockClient.SharedCountUpdates.ToArray());
    }

    [TestMethod]
    public async Task RapidSnapshotChanges_LeaveLatestCountsAdvertised()
    {
        var settings = new EngineSettings { Username = "user", Password = "pass" };
        var source = new FakeShareSource(new SoulseekShareSourceSnapshot(1, 1, []));
        var mockClient = new MockSoulseekClient(new(), initialState: SoulseekClientStates.None);
        using var manager = new SoulseekClientManager(settings, mockClient, source);
        await manager.EnsureConnectedAndLoggedInAsync(settings);

        var firstUpdateStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstUpdate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        mockClient.BeforeSetSharedCountsAsync = async (directories, _) =>
        {
            if (directories != 2)
                return;

            firstUpdateStarted.TrySetResult();
            await releaseFirstUpdate.Task;
        };

        source.SetSnapshot(new SoulseekShareSourceSnapshot(2, 2, []));
        await firstUpdateStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        source.SetSnapshot(new SoulseekShareSourceSnapshot(3, 3, []));
        releaseFirstUpdate.TrySetResult();

        await WaitForConditionAsync(() => mockClient.SharedCountUpdates.Count == 3, TimeSpan.FromSeconds(5));
        CollectionAssert.AreEqual(
            new[] { (1, 0), (2, 0), (3, 0) },
            mockClient.SharedCountUpdates.ToArray());
    }

    [TestMethod]
    public async Task ChatOperations_MapRoomsMessagesAcknowledgementAndPublicOptIn()
    {
        var mockClient = new MockSoulseekClient(new());
        mockClient.AvailablePublicChatRooms.Add(new RoomInfo("music", 12));
        mockClient.AvailablePrivateChatRooms.Add(new RoomInfo("friends", 3));
        using var manager = new SoulseekClientManager(new EngineSettings(), mockClient);
        SoulseekPrivateChatMessageEventArgs? privateMessage = null;
        SoulseekRoomChatMessageEventArgs? roomMessage = null;
        SoulseekPublicChatMessageEventArgs? publicMessage = null;
        manager.PrivateChatMessageReceived += (_, message) => privateMessage = message;
        manager.RoomChatMessageReceived += (_, message) => roomMessage = message;
        manager.PublicChatMessageReceived += (_, message) => publicMessage = message;

        var rooms = await manager.GetChatRoomsAsync();
        var joined = await manager.JoinChatRoomAsync(" music ");
        await manager.SendPrivateChatMessageAsync(" listener ", " hello ");
        await manager.SendRoomChatMessageAsync(" music ", " room hello ");
        await manager.StartPublicChatAsync();
        mockClient.RaisePrivateMessage(7, new DateTime(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc), " sender ", " private ", true);
        mockClient.RaiseRoomMessage(" music ", " member ", " room event ");
        mockClient.RaisePublicChatMessage(" ambient ", " broadcaster ", " public event ");
        await manager.LeaveChatRoomAsync(" music ");
        await manager.StopPublicChatAsync();

        Assert.AreEqual(2, rooms.Count);
        Assert.AreEqual("music", rooms[0].Name);
        Assert.IsFalse(rooms[0].IsPrivate);
        Assert.AreEqual("music", joined.Name);
        CollectionAssert.AreEqual(new[] { ("listener", "hello") }, mockClient.SentPrivateMessages);
        CollectionAssert.AreEqual(new[] { ("music", "room hello") }, mockClient.SentRoomMessages);
        CollectionAssert.AreEqual(new[] { 7 }, mockClient.AcknowledgedPrivateMessageIds);
        Assert.IsFalse(mockClient.JoinedChatRooms.Contains("music"));
        Assert.IsFalse(mockClient.IsPublicChatEnabled);
        Assert.IsNotNull(privateMessage);
        Assert.AreEqual(7, privateMessage.ProtocolId);
        Assert.AreEqual("sender", privateMessage.Username);
        Assert.AreEqual("private", privateMessage.Message);
        Assert.IsTrue(privateMessage.Replayed);
        Assert.IsNotNull(roomMessage);
        Assert.AreEqual("music", roomMessage.RoomName);
        Assert.AreEqual("member", roomMessage.Username);
        Assert.IsNotNull(publicMessage);
        Assert.AreEqual("ambient", publicMessage.RoomName);
        Assert.AreEqual("broadcaster", publicMessage.Username);
    }

    [TestMethod]
    public void ChatEvents_IgnoreBlankValuesAndBoundIncomingContent()
    {
        var mockClient = new MockSoulseekClient(new());
        using var manager = new SoulseekClientManager(new EngineSettings(), mockClient);
        var privateMessages = new List<SoulseekPrivateChatMessageEventArgs>();
        var roomMessages = new List<SoulseekRoomChatMessageEventArgs>();
        manager.PrivateChatMessageReceived += (_, message) => privateMessages.Add(message);
        manager.RoomChatMessageReceived += (_, message) => roomMessages.Add(message);

        mockClient.RaisePrivateMessage(1, DateTime.UtcNow, " ", "ignored");
        mockClient.RaiseRoomMessage("room", "user", " ");
        mockClient.RaisePrivateMessage(
            2,
            DateTime.UtcNow,
            new string('u', 150),
            new string('m', 5000));

        Assert.AreEqual(1, privateMessages.Count);
        Assert.AreEqual(SoulseekClientManager.MaximumChatIdentityLength, privateMessages[0].Username.Length);
        Assert.AreEqual(SoulseekClientManager.MaximumChatMessageLength, privateMessages[0].Message.Length);
        Assert.AreEqual(0, roomMessages.Count);
    }

    [TestMethod]
    public async Task ChatOperations_RejectInvalidInputBeforeCallingClient()
    {
        var mockClient = new MockSoulseekClient(new());
        using var manager = new SoulseekClientManager(new EngineSettings(), mockClient);

        await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
            manager.SendPrivateChatMessageAsync(" ", "message"));
        await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
            manager.SendRoomChatMessageAsync("room", new string('m', SoulseekClientManager.MaximumChatMessageLength + 1)));
        await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(() =>
            manager.AcknowledgePrivateChatMessageAsync(-1));

        Assert.AreEqual(0, mockClient.SentPrivateMessages.Count);
        Assert.AreEqual(0, mockClient.SentRoomMessages.Count);
        Assert.AreEqual(0, mockClient.AcknowledgedPrivateMessageIds.Count);
    }

    [TestMethod]
    public async Task ChatOperations_MapRpcFailuresToConnectionUnavailable()
    {
        var mockClient = new MockSoulseekClient(new())
        {
            ChatException = new TimeoutException("private transport detail"),
        };
        using var manager = new SoulseekClientManager(new EngineSettings(), mockClient);

        await Assert.ThrowsExceptionAsync<SoulseekConnectionUnavailableException>(() =>
            manager.GetChatRoomsAsync());
        await Assert.ThrowsExceptionAsync<SoulseekConnectionUnavailableException>(() =>
            manager.SendPrivateChatMessageAsync("listener", "message"));
        await Assert.ThrowsExceptionAsync<SoulseekConnectionUnavailableException>(() =>
            manager.StartPublicChatAsync());
    }

    [TestMethod]
    public async Task PrivilegeOperations_MapStatusNotificationAndAcknowledgement()
    {
        var mockClient = new MockSoulseekClient(new()) { PrivilegeDays = 14 };
        using var manager = new SoulseekClientManager(new EngineSettings(), mockClient);
        SoulseekPrivilegeNotificationEventArgs? received = null;
        manager.PrivilegeNotificationReceived += (_, notification) => received = notification;

        int remainingDays = await manager.GetPrivilegesAsync();
        mockClient.RaisePrivilegeNotification("  donor-user  ", 42);
        await manager.AcknowledgePrivilegeNotificationAsync(42);

        Assert.AreEqual(14, remainingDays);
        Assert.IsNotNull(received);
        Assert.AreEqual("donor-user", received.Username);
        Assert.AreEqual(42, received.AcknowledgementId);
        Assert.IsTrue(received.RequiresAcknowledgement);
        CollectionAssert.AreEqual(new[] { 42 }, mockClient.AcknowledgedPrivilegeNotificationIds);
    }

    [TestMethod]
    public void PrivilegeNotification_IgnoresBlankAndBoundsUsername()
    {
        var mockClient = new MockSoulseekClient(new());
        using var manager = new SoulseekClientManager(new EngineSettings(), mockClient);
        var received = new List<SoulseekPrivilegeNotificationEventArgs>();
        manager.PrivilegeNotificationReceived += (_, notification) => received.Add(notification);

        mockClient.RaisePrivilegeNotification("   ", 1);
        mockClient.RaisePrivilegeNotification(new string('u', 200));

        Assert.AreEqual(1, received.Count);
        Assert.AreEqual(128, received.Single().Username.Length);
        Assert.IsFalse(received.Single().RequiresAcknowledgement);
    }

    [TestMethod]
    public async Task PrivilegeOperations_MapRpcFailuresToConnectionUnavailable()
    {
        var mockClient = new MockSoulseekClient(new())
        {
            PrivilegeException = new TimeoutException("server timeout"),
        };
        using var manager = new SoulseekClientManager(new EngineSettings(), mockClient);

        await Assert.ThrowsExceptionAsync<SoulseekConnectionUnavailableException>(() =>
            manager.GetPrivilegesAsync());
        await Assert.ThrowsExceptionAsync<SoulseekConnectionUnavailableException>(() =>
            manager.AcknowledgePrivilegeNotificationAsync(5));
    }

    [TestMethod]
    public void Dispose_DisposesUnderlyingClient()
    {
        var mockClient = new MockSoulseekClient(new());
        var manager = new SoulseekClientManager(new EngineSettings(), mockClient);

        // Before the fix, Dispose did not exist/do anything. 
        // Now it should tear down the monitor loop and invoke Dispose on the client.
        manager.Dispose();

        Assert.IsTrue(mockClient.IsDisposed, "Underlying ISoulseekClient should be disposed.");
    }

    [TestMethod]
    public async Task WaitUntilReadyAsync_FaultsAfterPermanentLoginFailure()
    {
        var settings = new EngineSettings
        {
            Username = "user",
            Password = "pass",
        };
        var mockClient = new MockSoulseekClient(
            new(),
            initialState: SoulseekClientStates.None)
        {
            ConnectException = new InvalidOperationException("listener port unavailable"),
        };
        var manager = new SoulseekClientManager(settings, mockClient);

        try
        {
            await Assert.ThrowsExceptionAsync<SoulseekConnectionUnavailableException>(
                () => manager.EnsureConnectedAndLoggedInAsync(settings));

            var waitTask = manager.WaitUntilReadyAsync();
            var completed = await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromMilliseconds(500)));

            Assert.AreSame(waitTask, completed, "Permanent login failures must wake readiness waiters instead of leaving the engine stuck forever.");
            await Assert.ThrowsExceptionAsync<SoulseekConnectionUnavailableException>(() => waitTask);
        }
        finally
        {
            manager.Dispose();
        }
    }

    [DataTestMethod]
    [DataRow(null, null, "Missing Soulseek username and password.")]
    [DataRow("bbbbbbb", null, "Missing Soulseek password.")]
    public async Task EnsureConnectedAndLoggedInAsync_FailsCleanly_WhenCredentialsAreMissing(
        string? username,
        string? password,
        string expectedMessage)
    {
        var settings = new EngineSettings
        {
            Username = username,
            Password = password,
        };
        var mockClient = new MockSoulseekClient(
            new(),
            initialState: SoulseekClientStates.None);
        var manager = new SoulseekClientManager(settings, mockClient);

        try
        {
            var ex = await Assert.ThrowsExceptionAsync<SoulseekConnectionUnavailableException>(
                () => manager.EnsureConnectedAndLoggedInAsync(settings));

            StringAssert.Contains(ex.Message, expectedMessage);
            Assert.IsFalse(ex.Message.Contains("--random-login", StringComparison.Ordinal), "Missing credential errors should not advertise --random-login.");
            Assert.AreEqual(0, mockClient.ConnectCallCount, "Missing credentials should fail before ConnectAsync.");
        }
        finally
        {
            manager.Dispose();
        }
    }

    [TestMethod]
    public async Task KickedFromServer_MarksFatal_WhenAutoReconnectDisabled()
    {
        var settings = new EngineSettings();
        var mockClient = new MockSoulseekClient(new());
        var manager = new SoulseekClientManager(settings, mockClient);

        try
        {
            mockClient.RaiseKickedFromServer(disconnect: false);

            Assert.IsTrue(manager.HasFatalError);
            await Assert.ThrowsExceptionAsync<SoulseekConnectionUnavailableException>(
                () => manager.WaitUntilReadyAsync());
        }
        finally
        {
            manager.Dispose();
        }
    }

    [TestMethod]
    public void KickedFromServer_DoesNotMarkFatal_WhenAutoReconnectEnabled()
    {
        var settings = new EngineSettings
        {
            AutoReconnectAfterKickedFromServer = true,
        };
        var mockClient = new MockSoulseekClient(new());
        var manager = new SoulseekClientManager(settings, mockClient);

        try
        {
            mockClient.RaiseKickedFromServer();

            Assert.IsFalse(manager.HasFatalError);
        }
        finally
        {
            manager.Dispose();
        }
    }

    private static async Task WaitForConditionAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!predicate() && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Assert.IsTrue(predicate(), "The expected asynchronous Soulseek state was not reached in time.");
    }

    private sealed class FakeShareSource(SoulseekShareSourceSnapshot snapshot) : ISoulseekShareSource
    {
        private SoulseekShareSourceSnapshot snapshot = snapshot;

        public event EventHandler? SnapshotChanged;

        public SoulseekShareSourceSnapshot GetSnapshot() => snapshot;

        public Task<Stream> OpenReadAsync(
            string remoteFilename,
            long offset,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = snapshot.Files.Single(candidate => candidate.RemoteFilename == remoteFilename);
            var stream = new MemoryStream(new byte[file.Size], writable: false);
            stream.Seek(offset, SeekOrigin.Begin);
            return Task.FromResult<Stream>(stream);
        }

        public void SetSnapshot(SoulseekShareSourceSnapshot value)
        {
            snapshot = value;
            SnapshotChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
