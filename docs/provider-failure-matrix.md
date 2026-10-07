# Provider failure matrix

Sprint 15 tracks provider recovery behavior separately from happy-path playlist import. External providers remain playlist or metadata sources only.

| Provider | Scenario | Current behavior | Evidence |
| --- | --- | --- | --- |
| Spotify | Development-mode / allowlist rejection (`403`) | Server returns `provider_forbidden` with an allowlist/development-mode message. API clients add provider recovery guidance without suggesting playback or downloading. | `ProviderEndpointTests.GetProviderPlaylists_WhenSpotifyForbidden_ReturnsDevelopmentModeMessage`; `RemoteCliBackendTests.SockseekApiClient_AppErrorsIncludeProviderRecoveryGuidance` |
| Spotify | API rate limit (`429`) | Server returns `provider_rate_limited`; user can retry later without account data loss. API clients add retry-later guidance. | `ProviderEndpointTests.GetProviderPlaylists_WhenSpotifyRateLimited_ReturnsRetryableProviderError`; `SpotifyPlaylistSourceProviderTests.ListUserPlaylistsAsync_WhenRateLimited_ThrowsRetryableProviderException`; `RemoteCliBackendTests.SockseekApiClient_AppErrorsIncludeProviderRecoveryGuidance` |
| Spotify | Expired access token with refresh token | Provider refreshes token and retries the request. | `SpotifyPlaylistSourceProviderTests.ListUserPlaylistsAsync_WhenAccessTokenExpired_RefreshesAndRetries` |
| Spotify | Revoked/expired refresh token | Server returns `provider_reauthorization_required`, marks the account authorization expired and clients tell the user to reconnect. | `ProviderEndpointTests.GetProviderPlaylists_WhenSpotifyRefreshRevoked_MarksAccountAuthorizationExpired`; `RemoteCliBackendTests.SockseekApiClient_AppErrorsIncludeProviderRecoveryGuidance` |
| YouTube | Revoked/expired refresh token | Server returns `provider_reauthorization_required`, marks the account authorization expired and clients tell the user to reconnect. | `ProviderEndpointTests.GetProviderPlaylists_WhenYouTubeRefreshRevoked_MarksAccountAuthorizationExpired`; `RemoteCliBackendTests.SockseekApiClient_AppErrorsIncludeProviderRecoveryGuidance` |
| YouTube | API quota/rate limit (`429`) | Server returns `provider_rate_limited`; user can retry later without account data loss. API clients add retry-later guidance. | `ProviderEndpointTests.GetProviderPlaylists_WhenYouTubeRateLimited_ReturnsRetryableProviderError`; `YouTubePlaylistSourceProviderTests.ListUserPlaylistsAsync_WhenRateLimited_ThrowsRetryableProviderException`; `RemoteCliBackendTests.SockseekApiClient_AppErrorsIncludeProviderRecoveryGuidance` |
| YouTube | Expired access token with refresh token | Provider refreshes token and retries the request. | `YouTubePlaylistSourceProviderTests.ListUserPlaylistsAsync_WhenAccessTokenExpired_RefreshesAndRetries` |
| Bandcamp | Public page missing supported metadata | Server returns a localized provider error; no account or credential state is involved. | `BandcampPlaylistSourceProviderTests` malformed/unsupported fixture coverage; Bandcamp endpoint tests verify no credential/cookie usage. |
| MusicBrainz | `503` / backoff | Metadata client retries conservatively and respects rate limiting/cache behavior. | `MusicBrainzMetadataClientTests` 503/backoff and limiter/cache coverage. |

## UX expectations

- `provider_reauthorization_required`: tell the user to reconnect the provider account.
- `provider_forbidden`: explain provider-side allowlist/quota/development-mode limitation.
- `provider_rate_limited`: tell the user the provider is throttling requests and retry later.
- `provider_error`: keep the provider-specific message and correlation ID visible for diagnostics.

These errors must never suggest provider playback or provider downloading as a recovery path.
