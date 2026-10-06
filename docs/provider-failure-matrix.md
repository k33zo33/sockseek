# Provider failure matrix

Sprint 15 tracks provider recovery behavior separately from happy-path playlist import. External providers remain playlist or metadata sources only.

| Provider | Scenario | Current behavior | Evidence |
| --- | --- | --- | --- |
| Spotify | Development-mode / allowlist rejection (`403`) | Server returns `provider_forbidden` with an allowlist/development-mode message. Desktop surfaces the API error in the relevant workflow. | `ProviderEndpointTests.GetProviderPlaylists_WhenSpotifyForbidden_ReturnsDevelopmentModeMessage` |
| Spotify | API rate limit (`429`) | Server returns `provider_rate_limited`; user can retry later without account data loss. | `ProviderEndpointTests.GetProviderPlaylists_WhenSpotifyRateLimited_ReturnsRetryableProviderError`; `SpotifyPlaylistSourceProviderTests.ListUserPlaylistsAsync_WhenRateLimited_ThrowsRetryableProviderException` |
| Spotify | Expired access token with refresh token | Provider refreshes token and retries the request. | `SpotifyPlaylistSourceProviderTests.ListUserPlaylistsAsync_WhenAccessTokenExpired_RefreshesAndRetries` |
| Spotify | Revoked/expired refresh token | Server returns `provider_reauthorization_required` and marks the account authorization expired. | `ProviderEndpointTests.GetProviderPlaylists_WhenSpotifyRefreshRevoked_MarksAccountAuthorizationExpired` |
| YouTube | Revoked/expired refresh token | Server returns `provider_reauthorization_required` and marks the account authorization expired. | `ProviderEndpointTests.GetProviderPlaylists_WhenYouTubeRefreshRevoked_MarksAccountAuthorizationExpired` |
| YouTube | API quota/rate limit (`429`) | Server returns `provider_rate_limited`; user can retry later without account data loss. | `ProviderEndpointTests.GetProviderPlaylists_WhenYouTubeRateLimited_ReturnsRetryableProviderError`; `YouTubePlaylistSourceProviderTests.ListUserPlaylistsAsync_WhenRateLimited_ThrowsRetryableProviderException` |
| YouTube | Expired access token with refresh token | Provider refreshes token and retries the request. | `YouTubePlaylistSourceProviderTests.ListUserPlaylistsAsync_WhenAccessTokenExpired_RefreshesAndRetries` |
| Bandcamp | Public page missing supported metadata | Server returns a localized provider error; no account or credential state is involved. | `BandcampPlaylistSourceProviderTests` malformed/unsupported fixture coverage; Bandcamp endpoint tests verify no credential/cookie usage. |
| MusicBrainz | `503` / backoff | Metadata client retries conservatively and respects rate limiting/cache behavior. | `MusicBrainzMetadataClientTests` 503/backoff and limiter/cache coverage. |

## UX expectations

- `provider_reauthorization_required`: tell the user to reconnect the provider account.
- `provider_forbidden`: explain provider-side allowlist/quota/development-mode limitation.
- `provider_rate_limited`: tell the user the provider is throttling requests and retry later.
- `provider_error`: keep the provider-specific message and correlation ID visible for diagnostics.

These errors must never suggest provider playback or provider downloading as a recovery path.
