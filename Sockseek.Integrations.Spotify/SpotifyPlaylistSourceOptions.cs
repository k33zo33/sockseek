namespace Sockseek.Integrations.Spotify;

public sealed record SpotifyPlaylistSourceOptions
{
    public static IReadOnlyList<string> DefaultScopes { get; } =
    [
        "playlist-read-private",
        "playlist-read-collaborative",
    ];

    public required string ClientId { get; init; }

    public Uri AccountsBaseUri { get; init; } = new("https://accounts.spotify.com/");

    public Uri ApiBaseUri { get; init; } = new("https://api.spotify.com/v1/");

    public IReadOnlyList<string> Scopes { get; init; } = DefaultScopes;

    public SpotifyPlaylistSourceOptions Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ClientId);
        ArgumentNullException.ThrowIfNull(AccountsBaseUri);
        ArgumentNullException.ThrowIfNull(ApiBaseUri);
        if (Scopes.Count == 0)
            throw new ArgumentException("At least one Spotify scope is required.", nameof(Scopes));
        if (Scopes.Any(scope => string.IsNullOrWhiteSpace(scope)))
            throw new ArgumentException("Spotify scopes cannot contain blank values.", nameof(Scopes));

        return this;
    }
}
