using Microsoft.VisualStudio.TestTools.UnitTesting;
using Soulseek;
using Sockseek.Core.Services;
using Directory = System.IO.Directory;
using File = System.IO.File;

namespace Tests.Core;

[TestClass]
public class LocalFilesSoulseekClientTests
{
    [TestMethod]
    public async Task Privileges_ExposeStatusNotificationAndAcknowledgementForDeterministicWorkflows()
    {
        var client = new LocalFilesSoulseekClient([]) { PrivilegeDays = 7 };
        PrivilegeNotificationReceivedEventArgs? received = null;
        client.PrivilegeNotificationReceived += (_, notification) => received = notification;

        client.RaisePrivilegeNotification("supporter", 17);
        int remainingDays = await client.GetPrivilegesAsync();
        await client.AcknowledgePrivilegeNotificationAsync(17);

        Assert.IsNotNull(received);
        Assert.AreEqual("supporter", received.Username);
        Assert.AreEqual(17, received.Id);
        Assert.AreEqual(7, remainingDays);
        CollectionAssert.AreEqual(new[] { 17 }, client.AcknowledgedPrivilegeNotificationIds.ToArray());
    }

    [TestMethod]
    public async Task Chat_ProvidesDeterministicRoomPrivateAndPublicParity()
    {
        var client = new LocalFilesSoulseekClient([]);
        client.AvailablePublicChatRooms.Add(new RoomInfo("music", 4));
        PrivateMessageReceivedEventArgs? privateMessage = null;
        RoomMessageReceivedEventArgs? roomMessage = null;
        PublicChatMessageReceivedEventArgs? publicMessage = null;
        client.PrivateMessageReceived += (_, message) => privateMessage = message;
        client.RoomMessageReceived += (_, message) => roomMessage = message;
        client.PublicChatMessageReceived += (_, message) => publicMessage = message;

        var rooms = await client.GetRoomListAsync();
        await client.JoinRoomAsync("music");
        await client.SendPrivateMessageAsync("listener", "hello");
        await client.SendRoomMessageAsync("music", "hello room");
        await client.StartPublicChatAsync();
        client.RaisePrivateMessage(5, DateTime.UtcNow, "sender", "private");
        client.RaiseRoomMessage("music", "member", "room");
        client.RaisePublicChatMessage("ambient", "speaker", "public");
        await client.AcknowledgePrivateMessageAsync(5);

        Assert.AreEqual(1, rooms.PublicCount);
        Assert.IsTrue(client.JoinedChatRooms.Contains("music"));
        Assert.IsTrue(client.IsPublicChatEnabled);
        CollectionAssert.AreEqual(new[] { ("listener", "hello") }, client.SentPrivateMessages.ToArray());
        CollectionAssert.AreEqual(new[] { ("music", "hello room") }, client.SentRoomMessages.ToArray());
        CollectionAssert.AreEqual(new[] { 5 }, client.AcknowledgedPrivateMessageIds.ToArray());
        Assert.AreEqual("sender", privateMessage?.Username);
        Assert.AreEqual("member", roomMessage?.Username);
        Assert.AreEqual("speaker", publicMessage?.Username);

        await client.LeaveRoomAsync("music");
        await client.StopPublicChatAsync();
        Assert.IsFalse(client.JoinedChatRooms.Contains("music"));
        Assert.IsFalse(client.IsPublicChatEnabled);
    }

    [TestMethod]
    public async Task Upload_StreamsInputAndRaisesGlobalStateAndProgressEvents()
    {
        var client = new LocalFilesSoulseekClient([]);
        var states = new List<Transfer>();
        var progress = new List<Transfer>();
        client.TransferStateChanged += (_, change) => states.Add(change.Transfer);
        client.TransferProgressUpdated += (_, change) => progress.Add(change.Transfer);
        byte[] content = Enumerable.Range(0, 40_000).Select(index => (byte)(index % 251)).ToArray();

        Task<Transfer> completion = await client.EnqueueUploadAsync(
            "listener",
            "@@Music\\Album\\track.flac",
            content.Length,
            _ => Task.FromResult<Stream>(new MemoryStream(content, writable: false)),
            token: 77);
        var completed = await completion;

        Assert.IsTrue(completed.State.HasFlag(TransferStates.Completed));
        Assert.IsTrue(completed.State.HasFlag(TransferStates.Succeeded));
        Assert.AreEqual(content.Length, completed.BytesTransferred);
        Assert.IsTrue(states.Any(transfer => transfer.State == TransferStates.Queued));
        Assert.IsTrue(states.Any(transfer => transfer.State == TransferStates.InProgress));
        Assert.IsTrue(states.Any(transfer => transfer.State.HasFlag(TransferStates.Completed)));
        Assert.IsTrue(progress.Count >= 2);
        Assert.AreEqual(content.Length, progress[^1].BytesTransferred);
        Assert.AreEqual(0, client.Uploads.Count);
    }

    [TestMethod]
    public async Task Upload_ShortInputRaisesRedactedTerminalErrorStateAndClearsActiveTransfer()
    {
        var client = new LocalFilesSoulseekClient([]);
        Transfer? terminal = null;
        client.TransferStateChanged += (_, change) =>
        {
            if (change.Transfer.State.HasFlag(TransferStates.Completed))
                terminal = change.Transfer;
        };

        Task<Transfer> completion = await client.EnqueueUploadAsync(
            "listener",
            "@@Music\\short.flac",
            10,
            _ => Task.FromResult<Stream>(new MemoryStream([1, 2, 3], writable: false)),
            token: 78);

        await Assert.ThrowsExceptionAsync<EndOfStreamException>(async () => await completion);
        Assert.IsNotNull(terminal);
        Assert.IsTrue(terminal.State.HasFlag(TransferStates.Errored));
        Assert.AreEqual(0, client.Uploads.Count);
    }

    [TestMethod]
    public async Task FromLocalPaths_UsesSoulseekRelativeIdentityAndDownloadsFromLocalSource()
    {
        string root = Path.Combine(Path.GetTempPath(), "Sockseek-local-files-identity-" + Guid.NewGuid());
        string albumDir = Path.Combine(root, "Artist", "Album");
        string outputPath = Path.Combine(Path.GetTempPath(), "Sockseek-local-files-download-" + Guid.NewGuid() + ".mp3");
        Directory.CreateDirectory(albumDir);
        File.WriteAllBytes(Path.Combine(albumDir, "01. Artist - Track.mp3"), [1, 2, 3, 4]);

        try
        {
            var client = LocalFilesSoulseekClient.FromLocalPaths(useTags: false, slowMode: false, root);
            var result = await client.SearchAsync(new SearchQuery("Artist Track"));

            Assert.AreEqual(1, result.Responses.Count);
            Assert.AreEqual(@"Artist\Album\01. Artist - Track.mp3", result.Responses.First().Files.First().Filename);

            var transfer = await client.DownloadAsync("local", result.Responses.First().Files.First().Filename, outputPath);

            Assert.AreEqual(TransferStates.Completed, transfer.State);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(outputPath));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [TestMethod]
    public async Task FromLocalPaths_FailDownloads_FailsInitialDownloadAttempts()
    {
        string root = Path.Combine(Path.GetTempPath(), "Sockseek-local-files-fail-" + Guid.NewGuid());
        string outputPath = Path.Combine(Path.GetTempPath(), "Sockseek-local-files-fail-out-" + Guid.NewGuid() + ".mp3");
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, "Artist - Track.mp3"), [1, 2, 3, 4]);

        try
        {
            var client = LocalFilesSoulseekClient.FromLocalPaths(useTags: false, slowMode: false, failDownloads: 1, root);
            var result = await client.SearchAsync(new SearchQuery("Artist Track"));
            var filename = result.Responses.First().Files.First().Filename;

            await Assert.ThrowsExceptionAsync<SoulseekClientException>(async () =>
                await client.DownloadAsync("local", filename, outputPath));

            var transfer = await client.DownloadAsync("local", filename, outputPath);

            Assert.AreEqual(TransferStates.Completed, transfer.State);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(outputPath));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }
}
