using Soulseek;

namespace Tests.ClientTests;

public partial class MockSoulseekClient
{
    public event EventHandler<PrivateMessageReceivedEventArgs>? PrivateMessageReceived;
    public event EventHandler<RoomMessageReceivedEventArgs>? RoomMessageReceived;
    public event EventHandler<PublicChatMessageReceivedEventArgs>? PublicChatMessageReceived;

    public List<RoomInfo> AvailablePublicChatRooms { get; } = [];
    public List<RoomInfo> AvailablePrivateChatRooms { get; } = [];
    public List<int> AcknowledgedPrivateMessageIds { get; } = [];
    public List<(string Username, string Message)> SentPrivateMessages { get; } = [];
    public List<(string RoomName, string Message)> SentRoomMessages { get; } = [];
    public HashSet<string> JoinedChatRooms { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Exception? ChatException { get; set; }
    public bool IsPublicChatEnabled { get; private set; }

    public Task<RoomList> GetRoomListAsync(CancellationToken? cancellationToken = null)
    {
        cancellationToken?.ThrowIfCancellationRequested();
        return ChatException == null
            ? Task.FromResult(new RoomList(AvailablePublicChatRooms, AvailablePrivateChatRooms, [], []))
            : Task.FromException<RoomList>(ChatException);
    }

    public Task<RoomData> JoinRoomAsync(
        string roomName,
        bool isPrivate = false,
        CancellationToken? cancellationToken = null)
    {
        cancellationToken?.ThrowIfCancellationRequested();
        if (ChatException != null)
            return Task.FromException<RoomData>(ChatException);
        JoinedChatRooms.Add(roomName);
        return Task.FromResult(new RoomData(roomName, [], isPrivate, string.Empty, []));
    }

    public Task LeaveRoomAsync(string roomName, CancellationToken? cancellationToken = null)
    {
        cancellationToken?.ThrowIfCancellationRequested();
        if (ChatException != null)
            return Task.FromException(ChatException);
        JoinedChatRooms.Remove(roomName);
        return Task.CompletedTask;
    }

    public Task SendPrivateMessageAsync(
        string username,
        string message,
        CancellationToken? cancellationToken = null)
    {
        cancellationToken?.ThrowIfCancellationRequested();
        if (ChatException != null)
            return Task.FromException(ChatException);
        SentPrivateMessages.Add((username, message));
        return Task.CompletedTask;
    }

    public Task AcknowledgePrivateMessageAsync(
        int privateMessageId,
        CancellationToken? cancellationToken = null)
    {
        cancellationToken?.ThrowIfCancellationRequested();
        if (ChatException != null)
            return Task.FromException(ChatException);
        AcknowledgedPrivateMessageIds.Add(privateMessageId);
        return Task.CompletedTask;
    }

    public Task SendRoomMessageAsync(
        string roomName,
        string message,
        CancellationToken? cancellationToken = null)
    {
        cancellationToken?.ThrowIfCancellationRequested();
        if (ChatException != null)
            return Task.FromException(ChatException);
        SentRoomMessages.Add((roomName, message));
        return Task.CompletedTask;
    }

    public Task StartPublicChatAsync(CancellationToken? cancellationToken = null)
    {
        cancellationToken?.ThrowIfCancellationRequested();
        if (ChatException != null)
            return Task.FromException(ChatException);
        IsPublicChatEnabled = true;
        return Task.CompletedTask;
    }

    public Task StopPublicChatAsync(CancellationToken? cancellationToken = null)
    {
        cancellationToken?.ThrowIfCancellationRequested();
        if (ChatException != null)
            return Task.FromException(ChatException);
        IsPublicChatEnabled = false;
        return Task.CompletedTask;
    }

    public void RaisePrivateMessage(
        int id,
        DateTime timestampUtc,
        string username,
        string message,
        bool replayed = false)
        => PrivateMessageReceived?.Invoke(
            this,
            new PrivateMessageReceivedEventArgs(id, timestampUtc, username, message, replayed));

    public void RaiseRoomMessage(string roomName, string username, string message)
        => RoomMessageReceived?.Invoke(this, new RoomMessageReceivedEventArgs(roomName, username, message));

    public void RaisePublicChatMessage(string roomName, string username, string message)
        => PublicChatMessageReceived?.Invoke(this, new PublicChatMessageReceivedEventArgs(roomName, username, message));
}
