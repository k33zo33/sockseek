using System.Net;
using System.Net.Http.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopWishlistViewModelTests
{
    [TestMethod]
    public async Task RefreshAsync_MapsScheduleAndResultState()
    {
        var dto = Entry(
            query: "Artist Track",
            enabled: true,
            state: "Succeeded",
            resultCount: 3,
            nextSearchAtUtc: new DateTimeOffset(2026, 10, 18, 16, 30, 0, TimeSpan.Zero));
        var handler = new QueueHandler(_ => JsonResponse<IReadOnlyList<SoulseekWishlistEntryDto>>([dto]));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopWishlistViewModel(new SockseekApiClient(http));

        Assert.IsTrue(await viewModel.RefreshAsync());

        var entry = viewModel.Entries.Single();
        Assert.AreEqual("Artist Track", entry.Query);
        Assert.AreEqual("Succeeded - 3 results", entry.StatusSummary);
        Assert.IsTrue(entry.CanRun);
        Assert.IsTrue(viewModel.HasEntries);
        Assert.IsNull(viewModel.ErrorMessage);
    }

    [TestMethod]
    public async Task SaveAsync_RejectsInvalidInputWithoutCallingDaemon()
    {
        var handler = new QueueHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopWishlistViewModel(new SockseekApiClient(http))
        {
            Query = "query",
            IntervalMinutes = "4",
        };

        Assert.IsNull(await viewModel.SaveAsync());
        StringAssert.Contains(viewModel.ErrorMessage, "between 5 and 10080");
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task SaveAsync_NewEntryPostsThenReloadsAndClearsEditor()
    {
        var saved = Entry("Rare Track", enabled: true);
        var handler = new QueueHandler(
            _ => JsonResponse(saved),
            _ => JsonResponse<IReadOnlyList<SoulseekWishlistEntryDto>>([saved]));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopWishlistViewModel(new SockseekApiClient(http))
        {
            Query = "  Rare Track  ",
            IntervalMinutes = "45",
            Enabled = true,
        };

        var result = await viewModel.SaveAsync();

        Assert.IsNotNull(result);
        Assert.AreEqual(HttpMethod.Post, handler.Requests[0].Method);
        Assert.AreEqual("api/v1/soulseek/wishlist", handler.Requests[0].Path);
        StringAssert.Contains(handler.Requests[0].Body, "Rare Track");
        Assert.AreEqual(HttpMethod.Get, handler.Requests[1].Method);
        Assert.AreEqual(string.Empty, viewModel.Query);
        Assert.AreEqual("30", viewModel.IntervalMinutes);
        Assert.AreEqual("Wishlist entry added.", viewModel.OperationSummary);
    }

    [TestMethod]
    public async Task SaveAsync_SelectedEntryUsesPutAndPreservesTypedValues()
    {
        var existing = Entry("Original", enabled: false);
        var updated = Entry("Updated", enabled: true) with { Id = existing.Id, IntervalMinutes = 60 };
        var handler = new QueueHandler(
            _ => JsonResponse(updated),
            _ => JsonResponse<IReadOnlyList<SoulseekWishlistEntryDto>>([updated]));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopWishlistViewModel(new SockseekApiClient(http));
        viewModel.SelectedEntry = new DesktopWishlistEntryViewModel(existing);
        viewModel.Query = "Updated";
        viewModel.IntervalMinutes = "60";
        viewModel.Enabled = true;

        var result = await viewModel.SaveAsync();

        Assert.IsNotNull(result);
        Assert.AreEqual(HttpMethod.Put, handler.Requests[0].Method);
        Assert.AreEqual($"api/v1/soulseek/wishlist/{existing.Id}", handler.Requests[0].Path);
        Assert.AreEqual("Wishlist entry saved.", viewModel.OperationSummary);
    }

    [TestMethod]
    public async Task RunNowAsync_ServerConflictIsVisibleAndKeepsEntry()
    {
        var existing = new DesktopWishlistEntryViewModel(Entry("Active", enabled: true, state: "Searching"));
        var handler = new QueueHandler(_ => JsonResponse(
            new AppErrorDto("wishlist_not_runnable", "The wishlist entry must be enabled and have no active search.", "wish-correlation"),
            HttpStatusCode.Conflict));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopWishlistViewModel(new SockseekApiClient(http));

        Assert.IsFalse(await viewModel.RunNowAsync(existing));
        StringAssert.Contains(viewModel.ErrorMessage, "no active search");
        StringAssert.Contains(viewModel.ErrorMessage, "wish-correlation");
    }

    [TestMethod]
    public void Commands_RequireWishlistEntriesForRowActions()
    {
        var handler = new QueueHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopWishlistViewModel(new SockseekApiClient(http));
        var entry = new DesktopWishlistEntryViewModel(Entry("query", enabled: true));

        Assert.IsTrue(viewModel.RefreshCommand.CanExecute(null));
        Assert.IsTrue(viewModel.SaveCommand.CanExecute(null));
        Assert.IsTrue(viewModel.ClearSelectionCommand.CanExecute(null));
        Assert.IsTrue(viewModel.RunNowCommand.CanExecute(entry));
        Assert.IsTrue(viewModel.ToggleEnabledCommand.CanExecute(entry));
        Assert.IsTrue(viewModel.DeleteCommand.CanExecute(entry));
        Assert.IsFalse(viewModel.RunNowCommand.CanExecute(null));
        Assert.IsFalse(viewModel.DeleteCommand.CanExecute(new object()));
    }

    [TestMethod]
    public async Task RefreshAsync_DaemonUnavailableShowsRecoveryMessage()
    {
        var handler = new QueueHandler(_ => throw new HttpRequestException("connection refused"));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopWishlistViewModel(new SockseekApiClient(http));

        Assert.IsFalse(await viewModel.RefreshAsync());
        StringAssert.Contains(viewModel.ErrorMessage, "Wait for it to reconnect");
        Assert.IsFalse(viewModel.IsBusy);
    }

    [TestMethod]
    public async Task RefreshAsync_SelectedEntryRemovedByDaemonClearsEditor()
    {
        var handler = new QueueHandler(_ => JsonResponse<IReadOnlyList<SoulseekWishlistEntryDto>>([]));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5030/") };
        var viewModel = new DesktopWishlistViewModel(new SockseekApiClient(http))
        {
            SelectedEntry = new DesktopWishlistEntryViewModel(Entry("removed", enabled: false)),
        };

        Assert.IsTrue(await viewModel.RefreshAsync());
        Assert.IsNull(viewModel.SelectedEntry);
        Assert.AreEqual(string.Empty, viewModel.Query);
        Assert.AreEqual("30", viewModel.IntervalMinutes);
        Assert.IsTrue(viewModel.Enabled);
    }

    [TestMethod]
    public void WishlistSurface_IsBoundedVirtualizedAndKeepsActionsBelowMetadata()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Sockseek.Desktop",
            "DesktopShellMainWindow.axaml"));
        var xaml = File.ReadAllText(xamlPath);
        const string wishlistBinding = "<ListBox ItemsSource=\"{Binding Search.Wishlist.Entries}\"";
        int wishlistStart = xaml.IndexOf(wishlistBinding, StringComparison.Ordinal);
        int wishlistEnd = xaml.IndexOf("</ListBox>", wishlistStart, StringComparison.Ordinal);

        Assert.IsTrue(wishlistStart >= 0, "The Search workspace should expose the wishlist list.");
        Assert.IsTrue(wishlistEnd > wishlistStart, "The wishlist ListBox should have a complete template.");
        string wishlistXaml = xaml[wishlistStart..wishlistEnd];
        StringAssert.Contains(wishlistXaml, "MaxHeight=\"300\"");
        StringAssert.Contains(wishlistXaml, "<VirtualizingStackPanel />");
        StringAssert.Contains(wishlistXaml, "Grid.Row=\"1\"");
        StringAssert.Contains(wishlistXaml, "Content=\"Run now\"");
    }

    private static SoulseekWishlistEntryDto Entry(
        string query,
        bool enabled,
        string state = "NeverRun",
        int? resultCount = null,
        DateTimeOffset? nextSearchAtUtc = null)
    {
        var now = new DateTimeOffset(2026, 10, 18, 16, 0, 0, TimeSpan.Zero);
        return new SoulseekWishlistEntryDto(
            Guid.NewGuid(),
            query,
            30,
            enabled,
            state,
            now,
            now,
            nextSearchAtUtc,
            null,
            resultCount == null ? null : now,
            null,
            null,
            resultCount,
            null);
    }

    private static HttpResponseMessage JsonResponse<T>(T value, HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode) { Content = JsonContent.Create(value) };

    private sealed class QueueHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> responses = new(responses);

        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri?.PathAndQuery.TrimStart('/') ?? string.Empty,
                body));

            if (responses.Count == 0)
                throw new InvalidOperationException("No queued response is available.");

            return responses.Dequeue()(request);
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, string Path, string Body);
}
