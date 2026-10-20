using Soulseek;
using System.Security.Cryptography;
using System.Net.Sockets;
using Sockseek.Core.Settings;
using System.ComponentModel;

namespace Sockseek.Core.Services;

public static class SockseekSoulseekClientIdentity
{
    // Soulseek.NET requires each application to use a unique minor version.
    // Sockseek uses the 800850000-800859999 range.
    public const int MinorVersion = 800850000;
}

public sealed class SoulseekConnectionUnavailableException : InvalidOperationException
{
    public SoulseekConnectionUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class SoulseekPrivilegeNotificationEventArgs : EventArgs
{
    public SoulseekPrivilegeNotificationEventArgs(string username, int? acknowledgementId)
    {
        Username = username;
        AcknowledgementId = acknowledgementId;
    }

    public string Username { get; }
    public int? AcknowledgementId { get; }
    public bool RequiresAcknowledgement => AcknowledgementId.HasValue;
}

public sealed record SoulseekChatRoomInfo(string Name, int UserCount, bool IsPrivate);

public sealed record SoulseekJoinedRoom(string Name, bool IsPrivate, IReadOnlyList<string> Usernames);

public sealed class SoulseekPrivateChatMessageEventArgs : EventArgs
{
    public SoulseekPrivateChatMessageEventArgs(
        int protocolId,
        DateTimeOffset occurredAtUtc,
        string username,
        string message,
        bool replayed)
    {
        ProtocolId = protocolId;
        OccurredAtUtc = occurredAtUtc;
        Username = username;
        Message = message;
        Replayed = replayed;
    }

    public int ProtocolId { get; }
    public DateTimeOffset OccurredAtUtc { get; }
    public string Username { get; }
    public string Message { get; }
    public bool Replayed { get; }
}

public sealed class SoulseekRoomChatMessageEventArgs : EventArgs
{
    public SoulseekRoomChatMessageEventArgs(string roomName, string username, string message)
    {
        RoomName = roomName;
        Username = username;
        Message = message;
    }

    public string RoomName { get; }
    public string Username { get; }
    public string Message { get; }
}

public sealed class SoulseekPublicChatMessageEventArgs : EventArgs
{
    public SoulseekPublicChatMessageEventArgs(string roomName, string username, string message)
    {
        RoomName = roomName;
        Username = username;
        Message = message;
    }

    public string RoomName { get; }
    public string Username { get; }
    public string Message { get; }
}

public sealed record SoulseekUploadTransferSnapshot(
    string Username,
    string RemoteFilename,
    int Token,
    string State,
    long Size,
    long BytesTransferred,
    double BytesPerSecond,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    bool IsTerminal);

public sealed class SoulseekUploadChangedEventArgs(
    SoulseekUploadTransferSnapshot upload,
    bool stateChanged) : EventArgs
{
    public SoulseekUploadTransferSnapshot Upload { get; } = upload;
    public bool StateChanged { get; } = stateChanged;
}

public class SoulseekClientManager : IDisposable
{
    internal const int MaximumChatIdentityLength = 128;
    internal const int MaximumChatMessageLength = 4096;
    internal const int MaximumChatCollectionSize = 1000;
    internal const int MaximumUploadFilenameLength = 1024;
    private const string KickedFromServerMessage =
        "Soulseek server kicked this client, probably because the same account logged in elsewhere.";

    private readonly EngineSettings _initialSettings;
    private readonly ISoulseekShareSource? _shareSource;
    private ISoulseekClient? _client;
    private readonly SemaphoreSlim _initializationSemaphore = new SemaphoreSlim(1, 1);
    private readonly SemaphoreSlim _shareCountUpdateSemaphore = new SemaphoreSlim(1, 1);
    private TaskCompletionSource _readyTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object stateLock = new();
    private Exception? _fatalException;
    private CancellationTokenSource? _monitorCts;
    private Task? _monitorTask;
    private string _currentUsername = string.Empty;
    private bool _disposed;

    public ISoulseekClient? Client => _client;

    public event EventHandler<SoulseekPrivilegeNotificationEventArgs>? PrivilegeNotificationReceived;
    public event EventHandler<SoulseekPrivateChatMessageEventArgs>? PrivateChatMessageReceived;
    public event EventHandler<SoulseekRoomChatMessageEventArgs>? RoomChatMessageReceived;
    public event EventHandler<SoulseekPublicChatMessageEventArgs>? PublicChatMessageReceived;
    public event EventHandler<SoulseekUploadChangedEventArgs>? UploadChanged;
    public event EventHandler? SessionReady;

    public SoulseekClientStates State => _client?.State ?? SoulseekClientStates.None;

    public bool IsConnectedAndLoggedIn =>
        _client != null &&
        _client.State.HasFlag(SoulseekClientStates.Connected) &&
        _client.State.HasFlag(SoulseekClientStates.LoggedIn);

    public bool HasFatalError
    {
        get
        {
            lock (stateLock)
                return _fatalException != null;
        }
    }

    public Task WaitUntilReadyAsync(CancellationToken cancellationToken = default)
    {
        lock (stateLock)
        {
            if (_fatalException != null)
                return Task.FromException(_fatalException);
        }

        if (IsConnectedAndLoggedIn) return Task.CompletedTask;

        return _readyTcs.Task.WaitAsync(cancellationToken);
    }

    public SoulseekClientManager(
        EngineSettings initialSettings,
        ISoulseekClient? client = null,
        ISoulseekShareSource? shareSource = null)
    {
        _initialSettings = initialSettings ?? throw new ArgumentNullException(nameof(initialSettings));
        _shareSource = shareSource;
        if (_shareSource is not null)
            _shareSource.SnapshotChanged += OnShareSnapshotChanged;
        if (client != null)
        {
            _client = client;
            AttachClientEvents(_client);
            if (IsConnectedAndLoggedIn)
                _readyTcs.TrySetResult();
            StartMonitoring();
        }
    }

    private void AttachClientEvents(ISoulseekClient client)
    {
        client.KickedFromServer += OnKickedFromServer;
        client.PrivilegeNotificationReceived += OnPrivilegeNotificationReceived;
        client.PrivateMessageReceived += OnPrivateMessageReceived;
        client.RoomMessageReceived += OnRoomMessageReceived;
        client.PublicChatMessageReceived += OnPublicChatMessageReceived;
        client.TransferProgressUpdated += OnTransferProgressUpdated;
        client.TransferStateChanged += OnTransferStateChanged;
    }

    public IReadOnlyList<SoulseekUploadTransferSnapshot> GetUploadTransfers()
        => _client?.Uploads
            .Select(TryCreateUploadSnapshot)
            .Where(upload => upload is not null)
            .Select(upload => upload!)
            .ToArray()
            ?? [];

    private void OnTransferProgressUpdated(object? sender, TransferProgressUpdatedEventArgs args)
        => PublishUploadChanged(args.Transfer, stateChanged: false);

    private void OnTransferStateChanged(object? sender, TransferStateChangedEventArgs args)
        => PublishUploadChanged(args.Transfer, stateChanged: true);

    private void PublishUploadChanged(Transfer transfer, bool stateChanged)
    {
        var snapshot = TryCreateUploadSnapshot(transfer);
        if (snapshot is not null)
            UploadChanged?.Invoke(this, new SoulseekUploadChangedEventArgs(snapshot, stateChanged));
    }

    private static SoulseekUploadTransferSnapshot? TryCreateUploadSnapshot(Transfer transfer)
    {
        if (transfer.Direction != TransferDirection.Upload
            || !TryNormalizeIncoming(transfer.Username, MaximumChatIdentityLength, out var username)
            || !TryNormalizeIncoming(transfer.Filename, MaximumUploadFilenameLength, out var remoteFilename))
        {
            return null;
        }

        long size = Math.Max(0, transfer.Size);
        long bytesTransferred = Math.Clamp(transfer.BytesTransferred, 0, size);
        double bytesPerSecond = double.IsFinite(transfer.AverageSpeed)
            ? Math.Max(0, transfer.AverageSpeed)
            : 0;

        return new SoulseekUploadTransferSnapshot(
            username,
            remoteFilename,
            transfer.Token,
            transfer.State.ToString(),
            size,
            bytesTransferred,
            bytesPerSecond,
            ToUtcOffset(transfer.StartTime),
            ToUtcOffset(transfer.EndTime),
            IsTerminal(transfer.State));
    }

    private static bool IsTerminal(TransferStates state)
        => state.HasFlag(TransferStates.Completed)
            || state.HasFlag(TransferStates.Cancelled)
            || state.HasFlag(TransferStates.TimedOut)
            || state.HasFlag(TransferStates.Errored)
            || state.HasFlag(TransferStates.Rejected)
            || state.HasFlag(TransferStates.Aborted);

    private static DateTimeOffset? ToUtcOffset(DateTime? value)
    {
        if (value is not { } timestamp)
            return null;

        DateTime utc = timestamp.Kind switch
        {
            DateTimeKind.Utc => timestamp,
            DateTimeKind.Local => timestamp.ToUniversalTime(),
            _ => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc),
        };
        return new DateTimeOffset(utc);
    }

    private void OnPrivilegeNotificationReceived(object? sender, PrivilegeNotificationReceivedEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.Username))
            return;

        string username = args.Username.Trim();
        if (username.Length > MaximumChatIdentityLength)
            username = username[..MaximumChatIdentityLength];

        PrivilegeNotificationReceived?.Invoke(
            this,
            new SoulseekPrivilegeNotificationEventArgs(username, args.Id));
    }

    private void OnPrivateMessageReceived(object? sender, PrivateMessageReceivedEventArgs args)
    {
        if (args.Id < 0
            || !TryNormalizeIncoming(args.Username, MaximumChatIdentityLength, out var username)
            || !TryNormalizeIncoming(args.Message, MaximumChatMessageLength, out var message))
        {
            return;
        }

        var timestamp = args.Timestamp.Kind switch
        {
            DateTimeKind.Utc => args.Timestamp,
            DateTimeKind.Local => args.Timestamp.ToUniversalTime(),
            _ => DateTime.SpecifyKind(args.Timestamp, DateTimeKind.Utc),
        };
        try
        {
            PrivateChatMessageReceived?.Invoke(
                this,
                new SoulseekPrivateChatMessageEventArgs(
                    args.Id,
                    new DateTimeOffset(timestamp),
                    username,
                    message,
                    args.Replayed));
        }
        finally
        {
            _ = AcknowledgeReceivedPrivateMessageAsync(args.Id);
        }
    }

    private void OnRoomMessageReceived(object? sender, RoomMessageReceivedEventArgs args)
    {
        if (!TryNormalizeIncoming(args.RoomName, MaximumChatIdentityLength, out var roomName)
            || !TryNormalizeIncoming(args.Username, MaximumChatIdentityLength, out var username)
            || !TryNormalizeIncoming(args.Message, MaximumChatMessageLength, out var message))
        {
            return;
        }

        RoomChatMessageReceived?.Invoke(this, new SoulseekRoomChatMessageEventArgs(roomName, username, message));
    }

    private void OnPublicChatMessageReceived(object? sender, PublicChatMessageReceivedEventArgs args)
    {
        if (!TryNormalizeIncoming(args.RoomName, MaximumChatIdentityLength, out var roomName)
            || !TryNormalizeIncoming(args.Username, MaximumChatIdentityLength, out var username)
            || !TryNormalizeIncoming(args.Message, MaximumChatMessageLength, out var message))
        {
            return;
        }

        PublicChatMessageReceived?.Invoke(this, new SoulseekPublicChatMessageEventArgs(roomName, username, message));
    }

    private static bool TryNormalizeIncoming(string? value, int maximumLength, out string normalized)
    {
        normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            return false;
        if (normalized.Length > maximumLength)
            normalized = normalized[..maximumLength];
        return true;
    }

    private async Task AcknowledgeReceivedPrivateMessageAsync(int privateMessageId)
    {
        try
        {
            if (_client is not null)
                await _client.AcknowledgePrivateMessageAsync(privateMessageId);
        }
        catch (Exception)
        {
            // The server may replay the message; consumers deduplicate the bounded protocol id.
        }
    }

    private void OnKickedFromServer(object? sender, EventArgs e)
    {
        if (_initialSettings.AutoReconnectAfterKickedFromServer)
        {
            SockseekLog.Soulseek.Warn($"{KickedFromServerMessage} Reconnecting because daemon mode is active.");
            return;
        }

        SockseekLog.Soulseek.Error($"{KickedFromServerMessage} Stopping this run.");
        MarkFatal(new SoulseekConnectionUnavailableException(
            KickedFromServerMessage,
            new KickedFromServerException(KickedFromServerMessage)));
    }

    private void StartMonitoring()
    {
        if (HasFatalError) return;
        if (_monitorTask != null) return;
        _monitorCts = new CancellationTokenSource();
        _monitorTask = Task.Run(() => MonitorConnectionLoopAsync(_monitorCts.Token));
    }

    private void MarkFatal(Exception exception)
    {
        lock (stateLock)
        {
            _fatalException ??= exception;
            _readyTcs.TrySetException(_fatalException);
        }

        _monitorCts?.Cancel();
    }

    /// <summary>
    /// Ensures the Soulseek client is created, connected, and logged in.
    /// Uses the provided config for login credentials if login is needed.
    /// </summary>
    /// <param name="loginSettings">Configuration containing potentially updated credentials.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown if login fails after retries.</exception>
    /// <exception cref="OperationCanceledException">Thrown if cancelled.</exception>
    public async Task EnsureConnectedAndLoggedInAsync(EngineSettings loginSettings, CancellationToken cancellationToken = default)
    {
        if (IsConnectedAndLoggedIn) return;

        await _initializationSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (IsConnectedAndLoggedIn) return;
            cancellationToken.ThrowIfCancellationRequested();

            if (_client == null)
            {
                _client = CreateClientInstance(_initialSettings);
                AttachClientEvents(_client);
            }

            if (!IsConnectedAndLoggedIn)
            {
                var missingCredentialMessage = GetMissingCredentialMessage(loginSettings);
                if (missingCredentialMessage != null)
                    throw new InvalidOperationException(missingCredentialMessage);

                await LoginInternalAsync(_client, loginSettings, cancellationToken);
                _readyTcs.TrySetResult();
                StartMonitoring();
                SessionReady?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var failure = CreateConnectionFailure(ex);

            if (IsTransient(ex))
            {
                SockseekLog.Soulseek.Error(ex, "Failed to ensure Soulseek connection and login");
                StartMonitoring(); // Ensure monitoring starts even on transient failure so we can retry
            }
            else
            {
                MarkFatal(failure);
            }

            throw failure;
        }
        finally
        {
            _initializationSemaphore.Release();
        }
    }

    public async Task<int> GetPrivilegesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureConnectedAndLoggedInAsync(_initialSettings, cancellationToken);
            return await _client!.GetPrivilegesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SoulseekConnectionUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw CreateConnectionFailure(ex);
        }
    }

    public async Task AcknowledgePrivilegeNotificationAsync(
        int privilegeNotificationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(privilegeNotificationId);
        try
        {
            await EnsureConnectedAndLoggedInAsync(_initialSettings, cancellationToken);
            await _client!.AcknowledgePrivilegeNotificationAsync(privilegeNotificationId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SoulseekConnectionUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw CreateConnectionFailure(ex);
        }
    }

    public async Task<IReadOnlyList<SoulseekChatRoomInfo>> GetChatRoomsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureConnectedAndLoggedInAsync(_initialSettings, cancellationToken);
            var rooms = await _client!.GetRoomListAsync(cancellationToken);
            return MapChatRooms(rooms.Public, isPrivate: false)
                .Concat(MapChatRooms(rooms.Private, isPrivate: true))
                .OrderByDescending(room => room.UserCount)
                .ThenBy(room => room.Name, StringComparer.OrdinalIgnoreCase)
                .Take(MaximumChatCollectionSize)
                .ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SoulseekConnectionUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw CreateConnectionFailure(ex);
        }
    }

    public async Task<SoulseekJoinedRoom> JoinChatRoomAsync(
        string roomName,
        bool isPrivate = false,
        CancellationToken cancellationToken = default)
    {
        roomName = ValidateChatValue(roomName, nameof(roomName), MaximumChatIdentityLength);
        try
        {
            await EnsureConnectedAndLoggedInAsync(_initialSettings, cancellationToken);
            var room = await _client!.JoinRoomAsync(roomName, isPrivate, cancellationToken);
            string returnedName = TryNormalizeIncoming(room.Name, MaximumChatIdentityLength, out var normalizedName)
                ? normalizedName
                : roomName;
            return new SoulseekJoinedRoom(
                returnedName,
                room.IsPrivate,
                MapChatUsernames(room.Users.Select(user => user.Username))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(MaximumChatCollectionSize)
                    .ToArray());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SoulseekConnectionUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw CreateConnectionFailure(ex);
        }
    }

    public Task LeaveChatRoomAsync(string roomName, CancellationToken cancellationToken = default)
    {
        roomName = ValidateChatValue(roomName, nameof(roomName), MaximumChatIdentityLength);
        return ExecuteChatOperationAsync(
            client => client.LeaveRoomAsync(roomName, cancellationToken),
            cancellationToken);
    }

    public Task SendPrivateChatMessageAsync(
        string username,
        string message,
        CancellationToken cancellationToken = default)
    {
        username = ValidateChatValue(username, nameof(username), MaximumChatIdentityLength);
        message = ValidateChatValue(message, nameof(message), MaximumChatMessageLength);
        return ExecuteChatOperationAsync(
            client => client.SendPrivateMessageAsync(username, message, cancellationToken),
            cancellationToken);
    }

    public Task AcknowledgePrivateChatMessageAsync(
        int privateMessageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(privateMessageId);
        return ExecuteChatOperationAsync(
            client => client.AcknowledgePrivateMessageAsync(privateMessageId, cancellationToken),
            cancellationToken);
    }

    public Task SendRoomChatMessageAsync(
        string roomName,
        string message,
        CancellationToken cancellationToken = default)
    {
        roomName = ValidateChatValue(roomName, nameof(roomName), MaximumChatIdentityLength);
        message = ValidateChatValue(message, nameof(message), MaximumChatMessageLength);
        return ExecuteChatOperationAsync(
            client => client.SendRoomMessageAsync(roomName, message, cancellationToken),
            cancellationToken);
    }

    public Task StartPublicChatAsync(CancellationToken cancellationToken = default)
        => ExecuteChatOperationAsync(
            client => client.StartPublicChatAsync(cancellationToken),
            cancellationToken);

    public Task StopPublicChatAsync(CancellationToken cancellationToken = default)
        => ExecuteChatOperationAsync(
            client => client.StopPublicChatAsync(cancellationToken),
            cancellationToken);

    private async Task ExecuteChatOperationAsync(
        Func<ISoulseekClient, Task> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            await EnsureConnectedAndLoggedInAsync(_initialSettings, cancellationToken);
            await operation(_client!);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SoulseekConnectionUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw CreateConnectionFailure(ex);
        }
    }

    private static string ValidateChatValue(string? value, string parameterName, int maximumLength)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            throw new ArgumentException("Value cannot be empty.", parameterName);
        if (normalized.Length > maximumLength)
            throw new ArgumentException($"Value cannot exceed {maximumLength} characters.", parameterName);
        return normalized;
    }

    private static IEnumerable<SoulseekChatRoomInfo> MapChatRooms(
        IEnumerable<RoomInfo> rooms,
        bool isPrivate)
    {
        foreach (var room in rooms)
        {
            if (TryNormalizeIncoming(room.Name, MaximumChatIdentityLength, out var name))
                yield return new SoulseekChatRoomInfo(name, Math.Max(0, room.UserCount), isPrivate);
        }
    }

    private static IEnumerable<string> MapChatUsernames(IEnumerable<string> usernames)
    {
        foreach (var username in usernames)
        {
            if (TryNormalizeIncoming(username, MaximumChatIdentityLength, out var normalized))
                yield return normalized;
        }
    }

    private static SoulseekConnectionUnavailableException CreateConnectionFailure(Exception ex)
        => new(
            IsKickedFromServer(ex)
                ? KickedFromServerMessage
                : $"Soulseek login failed: {SockseekLog.ExceptionSummary(ex)}",
            ex);

    private bool IsTransient(Exception? e)
    {
        if (IsKickedFromServer(e))
            return _initialSettings.AutoReconnectAfterKickedFromServer;

        while (e != null)
        {
            if (e is Soulseek.AddressException || e is System.TimeoutException || e is System.Net.Sockets.SocketException) return true;
            if (e.GetType().Name.Contains("ConnectionException")) return true;
            if (e.GetType().Name.Contains("SoulseekClientException")) return true;
            e = e.InnerException;
        }
        return false;
    }

    private static bool IsKickedFromServer(Exception? e)
    {
        while (e != null)
        {
            if (e is KickedFromServerException || e.GetType().Name == nameof(KickedFromServerException)) return true;
            e = e.InnerException;
        }
        return false;
    }

    private string? GetMissingCredentialMessage(EngineSettings settings)
    {
        if (settings.UseRandomLogin
            || !string.IsNullOrWhiteSpace(settings.MockFilesDir)
            || !string.IsNullOrWhiteSpace(_initialSettings.MockFilesDir))
        {
            return null;
        }

        var missingUsername = string.IsNullOrWhiteSpace(settings.Username);
        var missingPassword = string.IsNullOrWhiteSpace(settings.Password);

        return (missingUsername, missingPassword) switch
        {
            (true, true) => "Missing Soulseek username and password. Provide --user and --pass, or configure username/password.",
            (true, false) => "Missing Soulseek username. Provide --user, or configure username.",
            (false, true) => "Missing Soulseek password. Provide --pass, or configure password.",
            _ => null,
        };
    }

    private async Task MonitorConnectionLoopAsync(CancellationToken ct)
    {
        int retryDelay = 1;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!IsConnectedAndLoggedIn)
                {
                    if (HasFatalError)
                        break;

                    if (_readyTcs.Task.IsCompleted)
                    {
                        _readyTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    }

                    SockseekLog.Soulseek.Warn($"Connection lost. Retrying in {retryDelay}s...");
                    await Task.Delay(retryDelay * 1000, ct);
                    
                    await EnsureConnectedAndLoggedInAsync(_initialSettings, ct);
                    retryDelay = 1; // Reset on success
                    SockseekLog.Soulseek.Info("Reconnected successfully.");
                }
                else
                {
                    retryDelay = 1;
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                if (!IsTransient(ex))
                {
                    MarkFatal(ex);
                    SockseekLog.Soulseek.Fatal(ex, "Permanent Soulseek error. Stopping reconnection attempts");
                    break;
                }

                SockseekLog.Soulseek.Debug(SockseekLog.FormatException("Reconnection attempt failed", ex));
                retryDelay = Math.Min(retryDelay * 2, 8);
            }

            await Task.Delay(1000, ct);
        }
    }

    private ISoulseekClient CreateClientInstance(EngineSettings settings)
    {
        SockseekLog.Soulseek.Debug("Creating Soulseek client instance...");
        if (!string.IsNullOrEmpty(settings.MockFilesDir))
        {
            SockseekLog.Soulseek.Info("Using local files Soulseek client.");
            return LocalFilesSoulseekClient.FromLocalPaths(
                settings.MockFilesReadTags,
                settings.MockFilesSlow,
                settings.MockFilesFailDownloads,
                settings.MockFilesDir);
        }
        else
        {
            SockseekLog.Soulseek.Debug("Configuring Soulseek Client connection options.");
            int startingToken = CreateRandomStartingToken();
            SockseekLog.Soulseek.Debug($"Using Soulseek client starting token {startingToken}.");
            return new SoulseekClient(
                SockseekSoulseekClientIdentity.MinorVersion,
                CreateClientOptions(
                    settings,
                    startingToken,
                    _shareSource,
                    () => _currentUsername,
                    () => _client));
        }
    }

    internal static int CreateRandomStartingToken()
        => RandomNumberGenerator.GetInt32(1, int.MaxValue);

    internal static SoulseekClientOptions CreateClientOptions(
        EngineSettings settings,
        int startingToken,
        ISoulseekShareSource? shareSource = null,
        Func<string>? localUsername = null,
        Func<ISoulseekClient?>? client = null)
    {
        var serverConnectionOptions = new ConnectionOptions(
            connectTimeout: settings.ConnectTimeout,
            configureSocket: (socket) =>
            {
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 15);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 15);
            });

        var transferConnectionOptions = new ConnectionOptions(
            inactivityTimeout: int.MaxValue, // this is handled by --max-stale-time
            configureSocket: (socket) =>
            {
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 15);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 15);
            });

        var sharingResponder = shareSource is null
            ? null
            : new SoulseekSharingResponder(
                shareSource,
                localUsername ?? (() => settings.Username ?? string.Empty),
                client ?? (() => null));

        Task<UserInfo> userInfoResolver(string username, System.Net.IPEndPoint ip)
            => sharingResponder is null
                ? Task.FromResult(new UserInfo(
                    description: settings.UserDescription ?? "",
                    uploadSlots: 1,
                    queueLength: 0,
                    hasFreeUploadSlot: true))
                : sharingResponder.ResolveUserInfoAsync(username, ip, settings.UserDescription);
        Func<string, int, SearchQuery, Task<SearchResponse>>? searchResponseResolver = sharingResponder is null
            ? null
            : sharingResponder.ResolveSearchAsync;
        Func<string, System.Net.IPEndPoint, Task<BrowseResponse>>? browseResponseResolver = sharingResponder is null
            ? null
            : sharingResponder.ResolveBrowseAsync;
        Func<string, System.Net.IPEndPoint, int, string, Task<IEnumerable<Soulseek.Directory>>>? directoryContentsResolver = sharingResponder is null
            ? null
            : sharingResponder.ResolveDirectoryContentsAsync;
        Func<string, System.Net.IPEndPoint, string, Task>? enqueueDownload = sharingResponder is null
            ? null
            : sharingResponder.EnqueueDownloadAsync;
        Func<string, System.Net.IPEndPoint, string, Task<int?>>? placeInQueueResolver = sharingResponder is null
            ? null
            : sharingResponder.ResolveQueuePositionAsync;

        var clientOptionsBuilder = new SoulseekClientOptions(
            transferConnectionOptions: transferConnectionOptions,
            serverConnectionOptions: serverConnectionOptions,
            listenPort: settings.ListenPort ?? 49998,
            maximumConcurrentSearches: int.MaxValue, // this is limited later in the searcher code
            maximumConcurrentUploads: 1,
            autoAcknowledgePrivateMessages: false,
            autoAcknowledgePrivilegeNotifications: false,
            searchResponseResolver: searchResponseResolver,
            browseResponseResolver: browseResponseResolver,
            directoryContentsResolver: directoryContentsResolver,
            userInfoResolver: userInfoResolver,
            enqueueDownload: enqueueDownload,
            placeInQueueResolver: placeInQueueResolver,
            startingToken: startingToken
        );

        if (settings.ListenPort == null)
        {
            // No listen port: create client without listener to avoid bind failures
            clientOptionsBuilder = new SoulseekClientOptions(
                transferConnectionOptions: transferConnectionOptions,
                serverConnectionOptions: serverConnectionOptions,
                enableListener: false,
                maximumConcurrentSearches: int.MaxValue,
                maximumConcurrentUploads: 1,
                autoAcknowledgePrivateMessages: false,
                autoAcknowledgePrivilegeNotifications: false,
                searchResponseResolver: searchResponseResolver,
                browseResponseResolver: browseResponseResolver,
                directoryContentsResolver: directoryContentsResolver,
                userInfoResolver: userInfoResolver,
                enqueueDownload: enqueueDownload,
                placeInQueueResolver: placeInQueueResolver,
                startingToken: startingToken
            );
        }

        return clientOptionsBuilder;
    }

    /// <summary>
    /// Internal login logic extracted from DownloaderApplication.
    /// </summary>
    private async Task LoginInternalAsync(ISoulseekClient client, EngineSettings settings, CancellationToken cancellationToken)
    {
        string user = settings.Username ?? "";
        string pass = settings.Password ?? "";

        if (settings.UseRandomLogin)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            user = RandomNumberGenerator.GetString(chars, 10);
            pass = RandomNumberGenerator.GetString(chars, 10);
            SockseekLog.Soulseek.Debug($"Generated random username: {user}");
        }

        string displayUser = settings.UseRandomLogin ? "[Random]" : user;
        _currentUsername = user;
        SockseekLog.Soulseek.Info($"Logging in as {displayUser}..");

        cancellationToken.ThrowIfCancellationRequested();
        await client.ConnectAsync(user, pass);

        if (!settings.NoModifyShareCount)
        {
            SockseekLog.Soulseek.Debug($"Setting share count for {displayUser}");
            var snapshot = _shareSource?.GetSnapshot();
            int directories = snapshot?.DirectoryCount ?? settings.SharedFolders;
            int files = snapshot?.FileCount ?? settings.SharedFiles;
            await client.SetSharedCountsAsync(directories, files, cancellationToken);
        }
        SockseekLog.Soulseek.Debug($"Logged in as {displayUser}");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _monitorCts?.Cancel();
        if (_shareSource is not null)
            _shareSource.SnapshotChanged -= OnShareSnapshotChanged;
        if (_client != null)
        {
            _client.KickedFromServer -= OnKickedFromServer;
            _client.PrivilegeNotificationReceived -= OnPrivilegeNotificationReceived;
            _client.PrivateMessageReceived -= OnPrivateMessageReceived;
            _client.RoomMessageReceived -= OnRoomMessageReceived;
            _client.PublicChatMessageReceived -= OnPublicChatMessageReceived;
            _client.TransferProgressUpdated -= OnTransferProgressUpdated;
            _client.TransferStateChanged -= OnTransferStateChanged;
        }
        _client?.Dispose();
        _monitorCts?.Dispose();
        _initializationSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnShareSnapshotChanged(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        if (_disposed || _initialSettings.NoModifyShareCount || !IsConnectedAndLoggedIn)
            return;

        _ = RefreshSharedCountsAsync();
    }

    private async Task RefreshSharedCountsAsync()
    {
        try
        {
            await _shareCountUpdateSemaphore.WaitAsync();
            try
            {
                if (_disposed)
                    return;

                var client = _client;
                var snapshot = _shareSource?.GetSnapshot();
                if (client is null || snapshot is null || !IsConnectedAndLoggedIn)
                    return;

                await client.SetSharedCountsAsync(snapshot.DirectoryCount, snapshot.FileCount);
            }
            finally
            {
                _shareCountUpdateSemaphore.Release();
            }
        }
        catch (Exception exception) when (!_disposed)
        {
            SockseekLog.Soulseek.Warn($"Failed to refresh advertised share counts: {exception.GetType().Name}");
        }
    }
}
