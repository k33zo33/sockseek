using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;
using Sockseek.Server;
using System.Text.Json;

namespace Tests.Server;

[TestClass]
public class ServerEventCatalogTests
{
    [TestMethod]
    public void Describe_ClassifiesSnapshotInvalidationEventsAsState()
    {
        var descriptor = ServerEventCatalog.Describe("job.upserted");

        Assert.AreEqual(ServerEventCatalog.StateCategory, descriptor.Category);
        Assert.IsTrue(descriptor.SnapshotInvalidation);
        Assert.AreEqual(nameof(JobSummaryDto), descriptor.PayloadDto);
    }

    [TestMethod]
    public void Describe_ClassifiesPrivilegeNotificationAsSnapshotInvalidationWithoutPrivatePayload()
    {
        var descriptor = ServerEventCatalog.Describe("soulseek.privilege-state.changed");

        Assert.AreEqual(ServerEventCatalog.StateCategory, descriptor.Category);
        Assert.IsTrue(descriptor.SnapshotInvalidation);
        Assert.AreEqual(nameof(SoulseekPrivilegeStateChangedEventDto), descriptor.PayloadDto);
    }

    [TestMethod]
    public void PrivilegeStateChangedEvent_RehydratesWithoutUsernamePayload()
    {
        var notificationId = Guid.NewGuid();
        var payload = JsonSerializer.SerializeToElement(
            new SoulseekPrivilegeStateChangedEventDto(notificationId, true),
            SockseekApiJson.CreateSerializerOptions());
        var envelope = new ServerEventEnvelopeDto(
            1,
            "soulseek.privilege-state.changed",
            DateTimeOffset.UtcNow,
            ServerEventCatalog.StateCategory,
            SnapshotInvalidation: true,
            WorkflowId: null,
            payload);

        var converted = ServerEventPayloadConverter.RehydrateEnvelope(envelope);

        var changed = converted.Payload as SoulseekPrivilegeStateChangedEventDto;
        Assert.IsNotNull(changed);
        Assert.AreEqual(notificationId, changed.NotificationId);
        Assert.IsFalse(payload.TryGetProperty("username", out _));
    }

    [TestMethod]
    public void ChatStateChangedEvent_RehydratesWithoutMessageOrUsernamePayload()
    {
        var payload = JsonSerializer.SerializeToElement(
            new SoulseekChatStateChangedEventDto(9, "message-received"),
            SockseekApiJson.CreateSerializerOptions());
        var envelope = new ServerEventEnvelopeDto(
            9,
            "soulseek.chat-state.changed",
            DateTimeOffset.UtcNow,
            ServerEventCatalog.StateCategory,
            SnapshotInvalidation: true,
            WorkflowId: null,
            payload);

        var converted = ServerEventPayloadConverter.RehydrateEnvelope(envelope);

        Assert.IsInstanceOfType<SoulseekChatStateChangedEventDto>(converted.Payload);
        var changed = (SoulseekChatStateChangedEventDto)converted.Payload;
        Assert.AreEqual(9, changed.Revision);
        Assert.AreEqual("message-received", changed.Reason);
        Assert.IsFalse(payload.TryGetProperty("message", out _));
        Assert.IsFalse(payload.TryGetProperty("username", out _));
    }

    [TestMethod]
    public void Describe_ClassifiesDownloadProgressSeparatelyFromState()
    {
        var descriptor = ServerEventCatalog.Describe("download.progress");

        Assert.AreEqual(ServerEventCatalog.ProgressCategory, descriptor.Category);
        Assert.IsFalse(descriptor.SnapshotInvalidation);
        Assert.AreEqual(nameof(DownloadProgressEventDto), descriptor.PayloadDto);
    }

    [TestMethod]
    public void Describe_ClassifiesLowLevelDownloadEventsAsActivity()
    {
        var descriptor = ServerEventCatalog.Describe("song.searching");

        Assert.AreEqual(ServerEventCatalog.ActivityCategory, descriptor.Category);
        Assert.IsFalse(descriptor.SnapshotInvalidation);
        Assert.AreEqual(nameof(SongSearchingEventDto), descriptor.PayloadDto);
    }
}
