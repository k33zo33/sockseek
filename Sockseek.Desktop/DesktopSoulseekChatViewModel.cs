using System.Windows.Input;
using Sockseek.Api;

namespace Sockseek.Desktop;

public sealed class DesktopSoulseekChatViewModel : ObservableObject
{
    private const int MaximumIdentityLength = 128;
    private const int MaximumMessageLength = 4096;

    private readonly SockseekApiClient apiClient;
    private IReadOnlyList<DesktopSoulseekChatRoomViewModel> availableRooms = [];
    private IReadOnlyList<DesktopSoulseekJoinedChatRoomViewModel> joinedRooms = [];
    private IReadOnlyList<DesktopSoulseekChatMessageViewModel> messages = [];
    private DesktopSoulseekChatRoomViewModel? selectedAvailableRoom;
    private DesktopSoulseekJoinedChatRoomViewModel? selectedJoinedRoom;
    private string roomName = string.Empty;
    private bool joinAsPrivate;
    private string privateUsername = string.Empty;
    private string privateMessage = string.Empty;
    private string roomMessage = string.Empty;
    private long revision;
    private bool publicChatEnabled;
    private bool isLoaded;
    private bool isBusy;
    private bool refreshPending;
    private string? errorMessage;
    private string? operationSummary;

    public DesktopSoulseekChatViewModel(SockseekApiClient apiClient)
    {
        this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        RefreshCommand = new DesktopAsyncCommand(() => RefreshAsync());
        RefreshRoomsCommand = new DesktopAsyncCommand(() => RefreshRoomsAsync());
        JoinSelectedRoomCommand = new DesktopAsyncParameterCommand<DesktopSoulseekChatRoomViewModel>(
            room => JoinRoomAsync(room.Name, room.IsPrivate));
        JoinRoomCommand = new DesktopAsyncCommand(() => JoinRoomAsync());
        LeaveRoomCommand = new DesktopAsyncParameterCommand<DesktopSoulseekJoinedChatRoomViewModel>(
            room => LeaveRoomAsync(room));
        SendPrivateMessageCommand = new DesktopAsyncCommand(() => SendPrivateMessageAsync());
        SendRoomMessageCommand = new DesktopAsyncCommand(() => SendRoomMessageAsync());
        TogglePublicChatCommand = new DesktopAsyncCommand(() => SetPublicChatEnabledAsync(!PublicChatEnabled));
    }

    public ICommand RefreshCommand { get; }

    public ICommand RefreshRoomsCommand { get; }

    public ICommand JoinSelectedRoomCommand { get; }

    public ICommand JoinRoomCommand { get; }

    public ICommand LeaveRoomCommand { get; }

    public ICommand SendPrivateMessageCommand { get; }

    public ICommand SendRoomMessageCommand { get; }

    public ICommand TogglePublicChatCommand { get; }

    public IReadOnlyList<DesktopSoulseekChatRoomViewModel> AvailableRooms
    {
        get => availableRooms;
        private set
        {
            if (!SetProperty(ref availableRooms, value))
                return;

            OnPropertyChanged(nameof(HasAvailableRooms));
            OnPropertyChanged(nameof(IsAvailableRoomsEmpty));
        }
    }

    public IReadOnlyList<DesktopSoulseekJoinedChatRoomViewModel> JoinedRooms
    {
        get => joinedRooms;
        private set
        {
            if (!SetProperty(ref joinedRooms, value))
                return;

            OnPropertyChanged(nameof(HasJoinedRooms));
            OnPropertyChanged(nameof(IsJoinedRoomsEmpty));
        }
    }

    public IReadOnlyList<DesktopSoulseekChatMessageViewModel> Messages
    {
        get => messages;
        private set
        {
            if (!SetProperty(ref messages, value))
                return;

            OnPropertyChanged(nameof(HasMessages));
            OnPropertyChanged(nameof(IsMessageTimelineEmpty));
        }
    }

    public DesktopSoulseekChatRoomViewModel? SelectedAvailableRoom
    {
        get => selectedAvailableRoom;
        set => SetProperty(ref selectedAvailableRoom, value);
    }

    public DesktopSoulseekJoinedChatRoomViewModel? SelectedJoinedRoom
    {
        get => selectedJoinedRoom;
        set
        {
            if (!SetProperty(ref selectedJoinedRoom, value))
                return;

            OnPropertyChanged(nameof(HasSelectedJoinedRoom));
            OnPropertyChanged(nameof(SelectedRoomSummary));
        }
    }

    public string RoomName
    {
        get => roomName;
        set => SetProperty(ref roomName, value ?? string.Empty);
    }

    public bool JoinAsPrivate
    {
        get => joinAsPrivate;
        set => SetProperty(ref joinAsPrivate, value);
    }

    public string PrivateUsername
    {
        get => privateUsername;
        set => SetProperty(ref privateUsername, value ?? string.Empty);
    }

    public string PrivateMessage
    {
        get => privateMessage;
        set => SetProperty(ref privateMessage, value ?? string.Empty);
    }

    public string RoomMessage
    {
        get => roomMessage;
        set => SetProperty(ref roomMessage, value ?? string.Empty);
    }

    public long Revision
    {
        get => revision;
        private set
        {
            if (!SetProperty(ref revision, value))
                return;

            OnPropertyChanged(nameof(RevisionSummary));
        }
    }

    public bool PublicChatEnabled
    {
        get => publicChatEnabled;
        private set
        {
            if (!SetProperty(ref publicChatEnabled, value))
                return;

            OnPropertyChanged(nameof(PublicChatActionLabel));
            OnPropertyChanged(nameof(PublicChatStatusSummary));
        }
    }

    public bool IsLoaded
    {
        get => isLoaded;
        private set
        {
            if (!SetProperty(ref isLoaded, value))
                return;

            OnPropertyChanged(nameof(IsAvailableRoomsEmpty));
            OnPropertyChanged(nameof(IsJoinedRoomsEmpty));
            OnPropertyChanged(nameof(IsMessageTimelineEmpty));
        }
    }

    public bool IsBusy
    {
        get => isBusy;
        private set => SetProperty(ref isBusy, value);
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    public string? OperationSummary
    {
        get => operationSummary;
        private set => SetProperty(ref operationSummary, value);
    }

    public bool HasAvailableRooms => AvailableRooms.Count > 0;

    public bool IsAvailableRoomsEmpty => IsLoaded && !HasAvailableRooms;

    public bool HasJoinedRooms => JoinedRooms.Count > 0;

    public bool IsJoinedRoomsEmpty => IsLoaded && !HasJoinedRooms;

    public bool HasMessages => Messages.Count > 0;

    public bool IsMessageTimelineEmpty => IsLoaded && !HasMessages;

    public bool HasSelectedJoinedRoom => SelectedJoinedRoom is not null;

    public string SelectedRoomSummary
        => SelectedJoinedRoom is null
            ? "Select a joined room"
            : $"Message #{SelectedJoinedRoom.Name}";

    public string RevisionSummary => IsLoaded ? $"Revision {Revision}" : "Chat state not loaded";

    public string PublicChatActionLabel => PublicChatEnabled ? "Stop public chat" : "Start public chat";

    public string PublicChatStatusSummary => PublicChatEnabled ? "Public chat active" : "Public chat stopped";

    public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            refreshPending = true;
            return false;
        }

        return await ExecuteAsync(async () =>
        {
            ApplySnapshot(await apiClient.GetSoulseekChatAsync(cancellationToken));
            return true;
        }, cancellationToken);
    }

    public async Task<bool> RefreshRoomsAsync(CancellationToken cancellationToken = default)
        => await ExecuteAsync(async () =>
        {
            ApplySnapshot(await apiClient.RefreshSoulseekChatRoomsAsync(cancellationToken));
            OperationSummary = "Room list refreshed.";
            return true;
        }, cancellationToken);

    public async Task<bool> JoinRoomAsync(CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeIdentity(RoomName, "Enter a room name.", out string normalizedRoomName))
            return false;

        bool joined = await JoinRoomAsync(normalizedRoomName, JoinAsPrivate, cancellationToken);
        if (joined)
        {
            RoomName = string.Empty;
            JoinAsPrivate = false;
        }

        return joined;
    }

    public async Task<bool> JoinRoomAsync(
        string roomName,
        bool isPrivate,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeIdentity(roomName, "Enter a room name.", out string normalizedRoomName))
            return false;

        return await ExecuteAsync(async () =>
        {
            ApplySnapshot(await apiClient.JoinSoulseekChatRoomAsync(
                new JoinSoulseekChatRoomRequestDto(normalizedRoomName, isPrivate),
                cancellationToken));
            SelectedJoinedRoom = JoinedRooms.FirstOrDefault(room =>
                StringComparer.OrdinalIgnoreCase.Equals(room.Name, normalizedRoomName));
            OperationSummary = "Room joined.";
            return true;
        }, cancellationToken);
    }

    public async Task<bool> LeaveRoomAsync(
        DesktopSoulseekJoinedChatRoomViewModel room,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(room);
        return await ExecuteAsync(async () =>
        {
            ApplySnapshot(await apiClient.LeaveSoulseekChatRoomAsync(
                new LeaveSoulseekChatRoomRequestDto(room.Name),
                cancellationToken));
            OperationSummary = "Room left.";
            return true;
        }, cancellationToken);
    }

    public async Task<bool> SendPrivateMessageAsync(CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeIdentity(PrivateUsername, "Enter a username.", out string username)
            || !TryValidateMessage(PrivateMessage, out string message))
        {
            return false;
        }

        return await ExecuteAsync(async () =>
        {
            ApplySnapshot(await apiClient.SendSoulseekPrivateChatMessageAsync(
                new SendSoulseekPrivateChatMessageRequestDto(username, message),
                cancellationToken));
            PrivateMessage = string.Empty;
            OperationSummary = "Private message sent.";
            return true;
        }, cancellationToken);
    }

    public async Task<bool> SendRoomMessageAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedJoinedRoom is null)
        {
            SetValidationError("Select a joined room before sending a message.");
            return false;
        }

        if (!TryValidateMessage(RoomMessage, out string message))
            return false;

        string selectedRoomName = SelectedJoinedRoom.Name;
        return await ExecuteAsync(async () =>
        {
            ApplySnapshot(await apiClient.SendSoulseekRoomChatMessageAsync(
                new SendSoulseekRoomChatMessageRequestDto(selectedRoomName, message),
                cancellationToken));
            RoomMessage = string.Empty;
            OperationSummary = "Room message sent.";
            return true;
        }, cancellationToken);
    }

    public async Task<bool> SetPublicChatEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
        => await ExecuteAsync(async () =>
        {
            ApplySnapshot(await apiClient.SetSoulseekPublicChatEnabledAsync(enabled, cancellationToken));
            OperationSummary = enabled ? "Public chat started." : "Public chat stopped.";
            return true;
        }, cancellationToken);

    private void ApplySnapshot(SoulseekChatSnapshotDto snapshot)
    {
        string? selectedAvailableRoomName = SelectedAvailableRoom?.Name;
        string? selectedJoinedRoomName = SelectedJoinedRoom?.Name;

        AvailableRooms = snapshot.AvailableRooms
            .Select(room => new DesktopSoulseekChatRoomViewModel(room))
            .ToArray();
        JoinedRooms = snapshot.JoinedRooms
            .Select(room => new DesktopSoulseekJoinedChatRoomViewModel(room))
            .ToArray();
        Messages = snapshot.Messages
            .Select(message => new DesktopSoulseekChatMessageViewModel(message))
            .ToArray();

        SelectedAvailableRoom = FindByName(AvailableRooms, selectedAvailableRoomName, room => room.Name);
        SelectedJoinedRoom = FindByName(JoinedRooms, selectedJoinedRoomName, room => room.Name) ?? JoinedRooms.FirstOrDefault();
        Revision = snapshot.Revision;
        PublicChatEnabled = snapshot.PublicChatEnabled;
        IsLoaded = true;
    }

    private bool TryNormalizeIdentity(string value, string emptyMessage, out string normalized)
    {
        ClearFeedback();
        normalized = value.Trim();
        if (normalized.Length == 0)
        {
            ErrorMessage = emptyMessage;
            return false;
        }

        if (normalized.Length > MaximumIdentityLength)
        {
            ErrorMessage = $"Names cannot exceed {MaximumIdentityLength} characters.";
            return false;
        }

        return true;
    }

    private bool TryValidateMessage(string value, out string message)
    {
        ClearFeedback();
        message = value;
        if (string.IsNullOrWhiteSpace(message))
        {
            ErrorMessage = "Enter a message.";
            return false;
        }

        if (message.Length > MaximumMessageLength)
        {
            ErrorMessage = $"Messages cannot exceed {MaximumMessageLength} characters.";
            return false;
        }

        return true;
    }

    private void SetValidationError(string message)
    {
        ClearFeedback();
        ErrorMessage = message;
    }

    private void ClearFeedback()
    {
        ErrorMessage = null;
        OperationSummary = null;
    }

    private async Task<bool> ExecuteAsync(Func<Task<bool>> action, CancellationToken cancellationToken)
    {
        if (IsBusy)
            return false;

        ClearFeedback();
        IsBusy = true;
        try
        {
            return await action();
        }
        catch (SockseekApiRequestException exception)
        {
            ErrorMessage = exception.Message;
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            ErrorMessage = "The local daemon did not respond in time. Retry the operation.";
            return false;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "The local daemon is unavailable. Wait for it to reconnect, then retry.";
            return false;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            ErrorMessage = "The local daemon returned an invalid chat response. Retry the operation.";
            return false;
        }
        finally
        {
            IsBusy = false;
            bool shouldRefresh = refreshPending;
            refreshPending = false;
            if (shouldRefresh && !cancellationToken.IsCancellationRequested)
            {
                _ = RefreshAsync();
            }
        }
    }

    private static T? FindByName<T>(
        IReadOnlyList<T> rooms,
        string? name,
        Func<T, string> getName)
        => name is null
            ? default
            : rooms.FirstOrDefault(room => StringComparer.OrdinalIgnoreCase.Equals(getName(room), name));
}

public sealed class DesktopSoulseekChatRoomViewModel
{
    public DesktopSoulseekChatRoomViewModel(SoulseekChatRoomDto room)
    {
        Name = room.Name;
        UserCount = room.UserCount;
        IsPrivate = room.IsPrivate;
    }

    public string Name { get; }

    public int UserCount { get; }

    public bool IsPrivate { get; }

    public string UserCountSummary => UserCount == 1 ? "1 user" : $"{UserCount} users";

    public string AccessSummary => IsPrivate ? "Private room" : "Public room";
}

public sealed class DesktopSoulseekJoinedChatRoomViewModel
{
    public DesktopSoulseekJoinedChatRoomViewModel(SoulseekJoinedChatRoomDto room)
    {
        Name = room.Name;
        IsPrivate = room.IsPrivate;
        Usernames = room.Usernames;
    }

    public string Name { get; }

    public bool IsPrivate { get; }

    public IReadOnlyList<string> Usernames { get; }

    public string ParticipantSummary
        => Usernames.Count == 1 ? "1 participant" : $"{Usernames.Count} participants";

    public string AccessSummary => IsPrivate ? "Private room" : "Public room";
}

public sealed class DesktopSoulseekChatMessageViewModel
{
    public DesktopSoulseekChatMessageViewModel(SoulseekChatMessageDto message)
    {
        Id = message.Id;
        Kind = message.Kind;
        RoomName = message.RoomName;
        Username = message.Username;
        Message = message.Message;
        OccurredAtUtc = message.OccurredAtUtc;
        IsOutgoing = message.IsOutgoing;
        Replayed = message.Replayed;
    }

    public Guid Id { get; }

    public string Kind { get; }

    public string? RoomName { get; }

    public string Username { get; }

    public string Message { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public bool IsOutgoing { get; }

    public bool Replayed { get; }

    public string ScopeSummary
        => Kind.ToLowerInvariant() switch
        {
            "private" => "Private",
            "public" => RoomName is { Length: > 0 } ? $"Public #{RoomName}" : "Public",
            _ => RoomName is { Length: > 0 } ? $"Room #{RoomName}" : "Room",
        };

    public string SenderSummary => IsOutgoing ? "You" : Username;

    public string OccurredSummary => OccurredAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public string DeliverySummary
        => (IsOutgoing, Replayed) switch
        {
            (true, true) => "Outgoing, replayed",
            (true, false) => "Outgoing",
            (false, true) => "Replayed",
            _ => "Received",
        };
}
