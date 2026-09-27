namespace Sockseek.Integrations.YouTube;

public sealed record YouTubePlaylistSourceOptions
{
    public static IReadOnlyList<string> DefaultScopes { get; } =
    [
        "https://www.googleapis.com/auth/youtube.readonly",
    ];

    public required string ClientId { get; init; }

    public Uri AuthorizationEndpointUri { get; init; } = new("https://accounts.google.com/o/oauth2/v2/auth");

    public Uri TokenEndpointUri { get; init; } = new("https://oauth2.googleapis.com/token");

    public Uri ApiBaseUri { get; init; } = new("https://www.googleapis.com/youtube/v3/");

    public IReadOnlyList<string> Scopes { get; init; } = DefaultScopes;

    public YouTubePlaylistSourceOptions Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ClientId);
        ArgumentNullException.ThrowIfNull(AuthorizationEndpointUri);
        ArgumentNullException.ThrowIfNull(TokenEndpointUri);
        ArgumentNullException.ThrowIfNull(ApiBaseUri);
        if (Scopes.Count == 0)
            throw new ArgumentException("At least one YouTube scope is required.", nameof(Scopes));
        if (Scopes.Any(scope => string.IsNullOrWhiteSpace(scope)))
            throw new ArgumentException("YouTube scopes cannot contain blank values.", nameof(Scopes));

        return this;
    }
}
