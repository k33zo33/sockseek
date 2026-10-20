using Sockseek.Api;
using Sockseek.Application.Common;
using Sockseek.Core;
using Sockseek.Core.Services;

namespace Sockseek.Server;

public sealed class SoulseekChatService : IDisposable
{
    internal const int MaximumRetainedMessages = 500;
    internal const int MaximumJoinedRooms = 20;
    private const int MaximumIdentityLength = 128;
    private const int MaximumMessageLength = 4096;

    private readonly EngineSupervisor supervisor;
    private readonly ServerEventBroadcaster broadcaster;
    private readonly IClock clock;
    private readonly Lock gate = new();
    private readonly SemaphoreSlim operationLock = new(1, 1);
    private readonly CancellationTokenSource lifetimeCts = new();
    private readonly LinkedList<ChatMessageState> messages = [];
    private readonly Dictionary<int, ChatMessageState> privateMessagesByProtocolId = [];
    private readonly Dictionary<string, SoulseekJoinedChatRoomDto> joinedRooms = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<SoulseekChatRoomDto> availableRooms = [];
    private bool publicChatEnabled;
    private long revision;
    private bool disposed;

    public SoulseekChatService(
        EngineSupervisor supervisor,
        ServerEventBroadcaster broadcaster,
        IClock clock)
    {
        this.supervisor = supervisor;
        this.broadcaster = broadcaster;
        this.clock = clock;
        supervisor.PrivateChatMessageReceived += OnPrivateChatMessageReceived;
        supervisor.RoomChatMessageReceived += OnRoomChatMessageReceived;
        supervisor.PublicChatMessageReceived += OnPublicChatMessageReceived;
        supervisor.EngineCreated += OnEngineCreated;
        supervisor.SoulseekSessionReady += OnSoulseekSessionReady;
    }

    public SoulseekChatSnapshotDto GetSnapshot()
    {
        lock (gate)
            return CreateSnapshotLocked();
    }

    public async Task<SoulseekChatSnapshotDto> RefreshRoomsAsync(CancellationToken cancellationToken = default)
    {
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            var rooms = await supervisor.GetChatRoomsAsync(cancellationToken);
            lock (gate)
            {
                availableRooms = rooms
                    .Select(room => new SoulseekChatRoomDto(room.Name, room.UserCount, room.IsPrivate))
                    .ToArray();
            }
        }
        finally
        {
            operationLock.Release();
        }

        PublishChanged("rooms-refreshed");
        return GetSnapshot();
    }

    public async Task<SoulseekChatSnapshotDto> JoinRoomAsync(
        string roomName,
        bool isPrivate,
        CancellationToken cancellationToken = default)
    {
        roomName = ValidateValue(roomName, nameof(roomName), MaximumIdentityLength);
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            lock (gate)
            {
                if (joinedRooms.ContainsKey(roomName))
                    return CreateSnapshotLocked();
                if (joinedRooms.Count >= MaximumJoinedRooms)
                    throw new InvalidOperationException($"At most {MaximumJoinedRooms} chat rooms can be joined.");
            }

            var joined = await supervisor.JoinChatRoomAsync(roomName, isPrivate, cancellationToken);
            lock (gate)
            {
                joinedRooms[joined.Name] = new SoulseekJoinedChatRoomDto(
                    joined.Name,
                    joined.IsPrivate,
                    joined.Usernames);
            }
        }
        finally
        {
            operationLock.Release();
        }

        PublishChanged("room-joined");
        return GetSnapshot();
    }

    public async Task<SoulseekChatSnapshotDto> LeaveRoomAsync(
        string roomName,
        CancellationToken cancellationToken = default)
    {
        roomName = ValidateValue(roomName, nameof(roomName), MaximumIdentityLength);
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            bool joined;
            lock (gate)
                joined = joinedRooms.ContainsKey(roomName);
            if (!joined)
                return GetSnapshot();

            await supervisor.LeaveChatRoomAsync(roomName, cancellationToken);
            lock (gate)
                joinedRooms.Remove(roomName);
        }
        finally
        {
            operationLock.Release();
        }

        PublishChanged("room-left");
        return GetSnapshot();
    }

    public async Task<SoulseekChatSnapshotDto> SendPrivateMessageAsync(
        string username,
        string message,
        CancellationToken cancellationToken = default)
    {
        username = ValidateValue(username, nameof(username), MaximumIdentityLength);
        message = ValidateValue(message, nameof(message), MaximumMessageLength);
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            await supervisor.SendPrivateChatMessageAsync(username, message, cancellationToken);
        }
        finally
        {
            operationLock.Release();
        }

        AddMessage(new ChatMessageState(
            Guid.NewGuid(),
            "private",
            null,
            username,
            message,
            clock.UtcNow,
            isOutgoing: true,
            replayed: false,
            protocolId: null), "message-sent");
        return GetSnapshot();
    }

    public async Task<SoulseekChatSnapshotDto> SendRoomMessageAsync(
        string roomName,
        string message,
        CancellationToken cancellationToken = default)
    {
        roomName = ValidateValue(roomName, nameof(roomName), MaximumIdentityLength);
        message = ValidateValue(message, nameof(message), MaximumMessageLength);
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            lock (gate)
            {
                if (!joinedRooms.ContainsKey(roomName))
                    throw new InvalidOperationException("Join the room before sending a message.");
            }

            await supervisor.SendRoomChatMessageAsync(roomName, message, cancellationToken);
        }
        finally
        {
            operationLock.Release();
        }

        AddMessage(new ChatMessageState(
            Guid.NewGuid(),
            "room",
            roomName,
            "You",
            message,
            clock.UtcNow,
            isOutgoing: true,
            replayed: false,
            protocolId: null), "message-sent");
        return GetSnapshot();
    }

    public async Task<SoulseekChatSnapshotDto> SetPublicChatEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            lock (gate)
            {
                if (publicChatEnabled == enabled)
                    return CreateSnapshotLocked();
            }

            if (enabled)
                await supervisor.StartPublicChatAsync(cancellationToken);
            else
                await supervisor.StopPublicChatAsync(cancellationToken);

            lock (gate)
                publicChatEnabled = enabled;
        }
        finally
        {
            operationLock.Release();
        }

        PublishChanged("public-chat-changed");
        return GetSnapshot();
    }

    private void OnPrivateChatMessageReceived(object? sender, SoulseekPrivateChatMessageEventArgs message)
    {
        bool added = false;
        lock (gate)
        {
            if (!privateMessagesByProtocolId.ContainsKey(message.ProtocolId))
            {
                var state = new ChatMessageState(
                    Guid.NewGuid(),
                    "private",
                    null,
                    message.Username,
                    message.Message,
                    message.OccurredAtUtc,
                    isOutgoing: false,
                    message.Replayed,
                    message.ProtocolId);
                AddMessageLocked(state);
                privateMessagesByProtocolId[message.ProtocolId] = state;
                added = true;
            }
        }

        if (added)
            PublishChanged("message-received");
    }

    private void OnRoomChatMessageReceived(object? sender, SoulseekRoomChatMessageEventArgs message)
        => AddMessage(new ChatMessageState(
            Guid.NewGuid(),
            "room",
            message.RoomName,
            message.Username,
            message.Message,
            clock.UtcNow,
            isOutgoing: false,
            replayed: false,
            protocolId: null), "message-received");

    private void OnPublicChatMessageReceived(object? sender, SoulseekPublicChatMessageEventArgs message)
        => AddMessage(new ChatMessageState(
            Guid.NewGuid(),
            "public",
            message.RoomName,
            message.Username,
            message.Message,
            clock.UtcNow,
            isOutgoing: false,
            replayed: false,
            protocolId: null), "message-received");

    private void AddMessage(ChatMessageState message, string reason)
    {
        lock (gate)
            AddMessageLocked(message);
        PublishChanged(reason);
    }

    private void AddMessageLocked(ChatMessageState message)
    {
        messages.AddLast(message);
        while (messages.Count > MaximumRetainedMessages)
        {
            var removed = messages.First!.Value;
            messages.RemoveFirst();
            if (removed.ProtocolId is { } protocolId)
                privateMessagesByProtocolId.Remove(protocolId);
        }
    }

    private void OnEngineCreated(DownloadEngine engine)
    {
        _ = engine;
        QueueRecovery();
    }

    private void OnSoulseekSessionReady(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        QueueRecovery();
    }

    private void QueueRecovery()
        => _ = RecoverNetworkStateAsync(lifetimeCts.Token);

    private async Task RecoverNetworkStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            bool restoredAny = false;
            await operationLock.WaitAsync(cancellationToken);
            try
            {
                SoulseekJoinedChatRoomDto[] rooms;
                bool restorePublicChat;
                lock (gate)
                {
                    rooms = joinedRooms.Values.ToArray();
                    restorePublicChat = publicChatEnabled;
                }

                restoredAny = rooms.Length > 0 || restorePublicChat;

                foreach (var room in rooms)
                {
                    var restored = await supervisor.JoinChatRoomAsync(room.Name, room.IsPrivate, cancellationToken);
                    lock (gate)
                    {
                        joinedRooms.Remove(room.Name);
                        joinedRooms[restored.Name] = new SoulseekJoinedChatRoomDto(
                            restored.Name,
                            restored.IsPrivate,
                            restored.Usernames);
                    }
                }

                if (restorePublicChat)
                    await supervisor.StartPublicChatAsync(cancellationToken);
            }
            finally
            {
                operationLock.Release();
            }

            if (restoredAny)
                PublishChanged("network-state-restored");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            // Desired state stays local and the next session-ready edge retries without private-data logs.
        }
    }

    private SoulseekChatSnapshotDto CreateSnapshotLocked()
        => new(
            revision,
            publicChatEnabled,
            availableRooms,
            joinedRooms.Values
                .OrderBy(room => room.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            messages.Select(ToDto).ToArray());

    private static SoulseekChatMessageDto ToDto(ChatMessageState message)
        => new(
            message.Id,
            message.Kind,
            message.RoomName,
            message.Username,
            message.Message,
            message.OccurredAtUtc,
            message.IsOutgoing,
            message.Replayed);

    private void PublishChanged(string reason)
    {
        long nextRevision;
        lock (gate)
            nextRevision = ++revision;
        broadcaster.PublishGlobal(
            "soulseek.chat-state.changed",
            new SoulseekChatStateChangedEventDto(nextRevision, reason));
    }

    private static string ValidateValue(string? value, string parameterName, int maximumLength)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            throw new ArgumentException("Value cannot be empty.", parameterName);
        if (normalized.Length > maximumLength)
            throw new ArgumentException($"Value cannot exceed {maximumLength} characters.", parameterName);
        return normalized;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        supervisor.PrivateChatMessageReceived -= OnPrivateChatMessageReceived;
        supervisor.RoomChatMessageReceived -= OnRoomChatMessageReceived;
        supervisor.PublicChatMessageReceived -= OnPublicChatMessageReceived;
        supervisor.EngineCreated -= OnEngineCreated;
        supervisor.SoulseekSessionReady -= OnSoulseekSessionReady;
        lifetimeCts.Cancel();
    }

    private sealed class ChatMessageState(
        Guid id,
        string kind,
        string? roomName,
        string username,
        string message,
        DateTimeOffset occurredAtUtc,
        bool isOutgoing,
        bool replayed,
        int? protocolId)
    {
        public Guid Id { get; } = id;
        public string Kind { get; } = kind;
        public string? RoomName { get; } = roomName;
        public string Username { get; } = username;
        public string Message { get; } = message;
        public DateTimeOffset OccurredAtUtc { get; } = occurredAtUtc;
        public bool IsOutgoing { get; } = isOutgoing;
        public bool Replayed { get; } = replayed;
        public int? ProtocolId { get; } = protocolId;
    }
}
