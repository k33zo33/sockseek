using Soulseek;

namespace Sockseek.Core.Services;

internal sealed class SoulseekSharingResponder
{
    internal const int MaximumSearchResults = 1000;
    private const int MaximumConcurrentUploads = 1;

    private readonly ISoulseekShareSource source;
    private readonly Func<string> localUsername;
    private readonly Func<ISoulseekClient?> client;

    public SoulseekSharingResponder(
        ISoulseekShareSource source,
        Func<string> localUsername,
        Func<ISoulseekClient?> client)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.localUsername = localUsername ?? throw new ArgumentNullException(nameof(localUsername));
        this.client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public Task<SearchResponse> ResolveSearchAsync(string username, int token, SearchQuery query)
    {
        _ = username;
        ArgumentNullException.ThrowIfNull(query);
        var snapshot = source.GetSnapshot();
        var matchingFiles = snapshot.Files
            .Where(file => Matches(query, file.RemoteFilename))
            .Take(MaximumSearchResults)
            .Select(ToSoulseekFile)
            .ToArray();
        var availability = GetUploadAvailability();
        return Task.FromResult(new SearchResponse(
            localUsername(),
            token,
            availability.HasFreeSlot,
            uploadSpeed: 0,
            queueLength: availability.QueueLength,
            fileList: matchingFiles));
    }

    public Task<BrowseResponse> ResolveBrowseAsync(string username, System.Net.IPEndPoint endpoint)
    {
        _ = username;
        _ = endpoint;
        return Task.FromResult(new BrowseResponse(CreateDirectories(source.GetSnapshot())));
    }

    public Task<IEnumerable<Soulseek.Directory>> ResolveDirectoryContentsAsync(
        string username,
        System.Net.IPEndPoint endpoint,
        int token,
        string directory)
    {
        _ = username;
        _ = endpoint;
        _ = token;
        string normalizedDirectory = directory.Replace('/', '\\');
        IEnumerable<Soulseek.Directory> result = CreateDirectories(source.GetSnapshot())
            .Where(item => StringComparer.Ordinal.Equals(item.Name, normalizedDirectory));
        return Task.FromResult(result);
    }

    public Task<UserInfo> ResolveUserInfoAsync(string username, System.Net.IPEndPoint endpoint, string? description)
    {
        _ = username;
        _ = endpoint;
        var availability = GetUploadAvailability();
        return Task.FromResult(new UserInfo(
            description ?? string.Empty,
            MaximumConcurrentUploads,
            availability.QueueLength,
            availability.HasFreeSlot));
    }

    public async Task EnqueueDownloadAsync(
        string username,
        System.Net.IPEndPoint endpoint,
        string remoteFilename)
    {
        _ = endpoint;
        var sharedFile = source.GetSnapshot().Files.FirstOrDefault(file =>
            StringComparer.Ordinal.Equals(file.RemoteFilename, remoteFilename.Replace('/', '\\')))
            ?? throw new FileNotFoundException("The requested shared file is unavailable.");
        var activeClient = client() ?? throw new InvalidOperationException("The Soulseek client is unavailable.");
        await activeClient.EnqueueUploadAsync(
            username,
            sharedFile.RemoteFilename,
            sharedFile.Size,
            offset => source.OpenReadAsync(sharedFile.RemoteFilename, offset),
            cancellationToken: CancellationToken.None);
    }

    public Task<int?> ResolveQueuePositionAsync(
        string username,
        System.Net.IPEndPoint endpoint,
        string remoteFilename)
    {
        _ = username;
        _ = endpoint;
        bool available = source.GetSnapshot().Files.Any(file =>
            StringComparer.Ordinal.Equals(file.RemoteFilename, remoteFilename.Replace('/', '\\')));
        return Task.FromResult<int?>(available ? 0 : null);
    }

    private IReadOnlyList<Soulseek.Directory> CreateDirectories(SoulseekShareSourceSnapshot snapshot)
        => snapshot.Files
            .GroupBy(file => RemoteDirectoryName(file.RemoteFilename), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new Soulseek.Directory(
                group.Key,
                group.OrderBy(file => file.RemoteFilename, StringComparer.Ordinal)
                    .Select(ToSoulseekFile)
                    .ToArray()))
            .ToArray();

    private static bool Matches(SearchQuery query, string remoteFilename)
        => query.Terms.All(term => remoteFilename.Contains(term, StringComparison.OrdinalIgnoreCase))
            && !query.Exclusions.Any(exclusion => remoteFilename.Contains(exclusion, StringComparison.OrdinalIgnoreCase));

    private static Soulseek.File ToSoulseekFile(SoulseekShareSourceFile file)
        => new(
            code: 1,
            filename: file.RemoteFilename,
            size: file.Size,
            extension: file.Extension);

    private (bool HasFreeSlot, int QueueLength) GetUploadAvailability()
    {
        int activeUploads = client()?.Uploads.Count ?? 0;
        return (
            activeUploads < MaximumConcurrentUploads,
            Math.Max(0, activeUploads - MaximumConcurrentUploads));
    }

    private static string RemoteDirectoryName(string remoteFilename)
    {
        int separator = remoteFilename.LastIndexOf('\\');
        return separator > 0 ? remoteFilename[..separator] : string.Empty;
    }
}
