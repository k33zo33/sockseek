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
}
