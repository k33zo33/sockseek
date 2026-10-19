using Sockseek.Api;
using Sockseek.Application.Common;
using Sockseek.Core.Services;

namespace Sockseek.Server;

public sealed class SoulseekRuntimeUnavailableException(string message) : InvalidOperationException(message);

public enum SoulseekPrivilegeAcknowledgementResult
{
    Acknowledged,
    NotFound,
    NotRequired,
}

public sealed class SoulseekPrivilegeService : IDisposable
{
    internal const int MaximumRetainedNotifications = 100;

    private readonly EngineSupervisor supervisor;
    private readonly ServerEventBroadcaster broadcaster;
    private readonly IClock clock;
    private readonly Lock gate = new();
    private readonly SemaphoreSlim acknowledgementLock = new(1, 1);
    private readonly LinkedList<PendingNotification> notifications = [];
    private readonly Dictionary<Guid, LinkedListNode<PendingNotification>> notificationsById = [];
    private readonly Dictionary<int, Guid> notificationsByAcknowledgementId = [];

    public SoulseekPrivilegeService(
        EngineSupervisor supervisor,
        ServerEventBroadcaster broadcaster,
        IClock clock)
    {
        this.supervisor = supervisor;
        this.broadcaster = broadcaster;
        this.clock = clock;
        supervisor.PrivilegeNotificationReceived += OnPrivilegeNotificationReceived;
    }

    public async Task<SoulseekPrivilegeStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        int remainingDays = await supervisor.GetPrivilegeDaysAsync(cancellationToken);
        lock (gate)
        {
            return new SoulseekPrivilegeStatusDto(
                Math.Max(0, remainingDays),
                clock.UtcNow,
                notifications.Select(ToDto).ToArray());
        }
    }

    public async Task<SoulseekPrivilegeAcknowledgementResult> AcknowledgeAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        await acknowledgementLock.WaitAsync(cancellationToken);
        try
        {
            int acknowledgementId;
            lock (gate)
            {
                if (!notificationsById.TryGetValue(notificationId, out var node))
                    return SoulseekPrivilegeAcknowledgementResult.NotFound;
                if (node.Value.AcknowledgementId is not { } protocolId)
                    return SoulseekPrivilegeAcknowledgementResult.NotRequired;
                acknowledgementId = protocolId;
            }

            await supervisor.AcknowledgePrivilegeNotificationAsync(acknowledgementId, cancellationToken);

            lock (gate)
            {
                if (notificationsById.TryGetValue(notificationId, out var node))
                    RemoveNode(node);
            }

            PublishChanged(notificationId, requiresAcknowledgement: false);
            return SoulseekPrivilegeAcknowledgementResult.Acknowledged;
        }
        finally
        {
            acknowledgementLock.Release();
        }
    }

    private void OnPrivilegeNotificationReceived(
        object? sender,
        SoulseekPrivilegeNotificationEventArgs notification)
    {
        int? acknowledgementId = notification.AcknowledgementId is >= 0
            ? notification.AcknowledgementId
            : null;
        Guid id;
        lock (gate)
        {
            if (acknowledgementId is { } protocolId
                && notificationsByAcknowledgementId.ContainsKey(protocolId))
            {
                return;
            }

            id = Guid.NewGuid();
            var pending = new PendingNotification(
                id,
                notification.Username,
                clock.UtcNow,
                acknowledgementId);
            var node = notifications.AddFirst(pending);
            notificationsById[id] = node;
            if (acknowledgementId is { } ackId)
                notificationsByAcknowledgementId[ackId] = id;

            while (notifications.Count > MaximumRetainedNotifications)
                RemoveNode(notifications.Last!);
        }

        PublishChanged(id, acknowledgementId.HasValue);
    }

    private void PublishChanged(Guid notificationId, bool requiresAcknowledgement)
        => broadcaster.PublishGlobal(
            "soulseek.privilege-state.changed",
            new SoulseekPrivilegeStateChangedEventDto(notificationId, requiresAcknowledgement));

    private void RemoveNode(LinkedListNode<PendingNotification> node)
    {
        notifications.Remove(node);
        notificationsById.Remove(node.Value.Id);
        if (node.Value.AcknowledgementId is { } acknowledgementId)
            notificationsByAcknowledgementId.Remove(acknowledgementId);
    }

    private static SoulseekPrivilegeNotificationDto ToDto(PendingNotification notification)
        => new(
            notification.Id,
            notification.Username,
            notification.ReceivedAtUtc,
            notification.AcknowledgementId.HasValue);

    public void Dispose()
    {
        supervisor.PrivilegeNotificationReceived -= OnPrivilegeNotificationReceived;
        acknowledgementLock.Dispose();
    }

    private sealed record PendingNotification(
        Guid Id,
        string Username,
        DateTimeOffset ReceivedAtUtc,
        int? AcknowledgementId);
}
