using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Domain.Soulseek;

namespace Sockseek.Domain.Tests.Soulseek;

[TestClass]
public sealed class SoulseekWishlistEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 10, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Create_EnabledEntry_IsImmediatelyDue()
    {
        var entry = SoulseekWishlistEntry.Create("  artist title  ", 30, enabled: true, Now);

        Assert.AreEqual("artist title", entry.Query);
        Assert.AreEqual(WishlistSearchState.NeverRun, entry.SearchState);
        Assert.AreEqual(Now, entry.NextSearchAtUtc);
        Assert.IsTrue(entry.CanStart(Now, force: false));
    }

    [TestMethod]
    public void MarkClaimed_PreventsDuplicateSearchUntilCompletion()
    {
        var entry = SoulseekWishlistEntry.Create("artist title", 30, enabled: true, Now);

        entry.MarkClaimed(Now);
        entry.MarkSubmitted(Guid.NewGuid(), Guid.NewGuid(), Now.AddSeconds(1));

        Assert.AreEqual(WishlistSearchState.Searching, entry.SearchState);
        Assert.IsFalse(entry.CanStart(Now.AddHours(1), force: true));
        Assert.AreEqual(Now.AddMinutes(30), entry.NextSearchAtUtc);

        entry.MarkSucceeded(4, Now.AddSeconds(10));

        Assert.AreEqual(WishlistSearchState.Succeeded, entry.SearchState);
        Assert.AreEqual(4, entry.LastResultCount);
        Assert.IsNull(entry.ActiveJobId);
        Assert.IsFalse(entry.CanStart(Now.AddMinutes(29), force: false));
        Assert.IsTrue(entry.CanStart(Now.AddMinutes(30), force: false));
    }

    [TestMethod]
    public void RecoverInterrupted_ReschedulesEnabledEntryImmediately()
    {
        var entry = SoulseekWishlistEntry.Create("artist title", 30, enabled: true, Now);
        entry.MarkClaimed(Now);
        entry.MarkSubmitted(Guid.NewGuid(), Guid.NewGuid(), Now);

        entry.RecoverInterrupted(Now.AddMinutes(2));

        Assert.AreEqual(WishlistSearchState.Failed, entry.SearchState);
        Assert.AreEqual("interrupted", entry.LastErrorCode);
        Assert.AreEqual(Now.AddMinutes(2), entry.NextSearchAtUtc);
        Assert.IsNull(entry.ActiveJobId);
    }

    [TestMethod]
    public void Update_DisablingSearchingEntry_CancelsAndClearsSchedule()
    {
        var entry = SoulseekWishlistEntry.Create("artist title", 30, enabled: true, Now);
        entry.MarkClaimed(Now);
        entry.MarkSubmitted(Guid.NewGuid(), Guid.NewGuid(), Now);

        entry.Update("artist title", 30, enabled: false, Now.AddMinutes(1));

        Assert.AreEqual(WishlistSearchState.Cancelled, entry.SearchState);
        Assert.IsFalse(entry.Enabled);
        Assert.IsNull(entry.NextSearchAtUtc);
        Assert.IsNull(entry.ActiveJobId);
    }

    [TestMethod]
    public void Update_ChangingActiveQuery_CancelsOldStateAndSchedulesReplacement()
    {
        var entry = SoulseekWishlistEntry.Create("artist title", 30, enabled: true, Now);
        entry.MarkClaimed(Now);
        entry.MarkSubmitted(Guid.NewGuid(), Guid.NewGuid(), Now);

        entry.Update("artist remaster", 60, enabled: true, Now.AddMinutes(1));

        Assert.AreEqual(WishlistSearchState.Cancelled, entry.SearchState);
        Assert.AreEqual("artist remaster", entry.Query);
        Assert.AreEqual(60, entry.IntervalMinutes);
        Assert.AreEqual(Now.AddMinutes(1), entry.NextSearchAtUtc);
        Assert.IsNull(entry.ActiveJobId);
    }

    [TestMethod]
    public void Create_RejectsUnboundedInput()
    {
        Assert.ThrowsException<ArgumentException>(() => SoulseekWishlistEntry.Create(" ", 30, true, Now));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => SoulseekWishlistEntry.Create("query", 4, true, Now));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => SoulseekWishlistEntry.Create("query", 10_081, true, Now));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => SoulseekWishlistEntry.Create(new string('x', 257), 30, true, Now));
    }
}
