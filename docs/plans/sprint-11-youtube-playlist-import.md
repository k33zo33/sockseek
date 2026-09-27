# Sprint 11 YouTube playlist import

## Goal

Implement YouTube as a playlist-source provider so connected users can authorize with Google installed-app OAuth, list their YouTube playlists and import/sync playlist metadata without introducing YouTube playback, background playback, audio extraction, audio URLs or download behavior.

## Current-state findings

- Sprint 9 provides the shared provider contract, PKCE/state coordinator, loopback OAuth callback support, `ISecretStore`, account lifecycle persistence and provider capability API/UI models.
- Sprint 10 added the first real provider (`Sockseek.Integrations.Spotify`) and the reusable server/API import endpoints:
  - `POST /api/v1/providers/{providerId}/authorization/start`
  - `POST /api/v1/providers/{providerId}/authorization/complete`
  - `GET /api/v1/accounts/{accountId}/provider-playlists`
  - `POST /api/v1/accounts/{accountId}/provider-playlists/{externalPlaylistId}/import`
- `ProviderIds.YouTube` and default YouTube capabilities already exist.
- No `Sockseek.Integrations.YouTube` project exists yet.
- The shared playlist snapshot persistence already supports stable provider item IDs, raw metadata JSON, external URLs, artwork URLs and Mirror sync preservation of local resolution state.
- Existing account status has `AuthorizationExpired`; Sprint 11 will use that as the current reauthorization-required state unless a separate API naming change is intentionally accepted.

## In scope

- Add a `Sockseek.Integrations.YouTube` project following the Spotify adapter shape.
- Implement Google installed-app OAuth with PKCE and the readonly `https://www.googleapis.com/auth/youtube.readonly` scope.
- Exchange authorization codes and refresh access tokens through `ISecretStore`; SQLite must store only opaque secret references.
- Use YouTube Data API fixture responses for:
  - channel/account identity,
  - `playlists.list?mine=true`,
  - `playlistItems.list`,
  - optional `videos.list` batch metadata lookup for durations and clearer unavailable/private/deleted item handling.
- Map video ID, title, channel, duration, thumbnail, YouTube URL and raw metadata into `ExternalTrackSnapshot`.
- Preserve deleted/private/unavailable videos as visible unresolved metadata items instead of crashing or dropping them.
- Handle pagination, 401 token expiry/refresh, refresh failure/revoke and 403/429 quota-facing errors.
- Wire YouTube provider registration into the existing server API only after adapter tests pass.
- Add policy tests and forbidden scans that prove no YouTube playback/download/audio capability was introduced.

## Out of scope

- YouTube playback, iframe player, embedded video player, background playback, audio extraction, yt-dlp, media URLs, preview URLs or download behavior.
- Write-back to YouTube playlists.
- Broad playlist resolution/download UI beyond preserving imported metadata for later local/Soulseek resolution.
- Bandcamp, MusicBrainz or ListenBrainz work.
- Live Google credentials in automated tests.

## Files and projects affected

- `Sockseek.Integrations.YouTube`: new adapter project, options, exception and provider implementation.
- `Sockseek.Application.Tests/Providers`: fixture-backed YouTube provider tests.
- `Sockseek.Server`: provider registration and provider-facing error mapping if YouTube needs provider-specific exception mapping.
- `Sockseek.Server.Tests/ProviderEndpointTests.cs`: fixture-backed authorization/list/import and token-expiry/revoke API tests.
- `Sockseek.sln` and `packages.lock.json`: new project wiring.
- `docs/openapi.json`: only if server DTOs or endpoint responses change; provider registration alone should not change OpenAPI.
- Sprint docs and plan updates as implementation discoveries happen.

## API, schema and event changes

- Prefer no new `/api/v1` contract; reuse Sprint 10 provider authorization/list/import endpoints for YouTube.
- No EF migration is expected; YouTube metadata should fit existing playlist item `SnapshotJson` and external playlist/account tables.
- If exact public status text `ReauthorizationRequired` is required instead of existing `AuthorizationExpired`, treat it as a deliberate public API change and update DTO tests/OpenAPI in the same commit.
- No new event contract is planned for Sprint 11.

## Implementation sequence

1. Add `Sockseek.Integrations.YouTube` project and solution wiring.
2. Implement options and authorization URI construction with only the readonly YouTube scope.
3. Implement token exchange, token refresh and secret persistence through `ISecretStore`.
4. Fetch channel/account identity with fixture-backed HTTP.
5. Implement `playlists.list?mine=true` pagination and playlist summary mapping.
6. Implement `playlistItems.list` pagination and item mapping for public, private, deleted and unavailable videos.
7. Add optional `videos.list` batch lookup for ISO 8601 duration parsing and richer metadata, keeping unavailable items visible when lookup data is absent.
8. Add adapter tests for OAuth, pagination, mapping, 401 refresh, refresh failure/revoke, 403 quota/policy and 429 retry-after.
9. Wire YouTube into `ServerHost` DI/config and reuse the existing authorization/list/import endpoints.
10. Add server fixture tests proving a user can import and Mirror-sync YouTube playlists without duplicate items.
11. Add static no-playback/no-download policy tests and run the forbidden provider-audio scan.
12. Run full validation and update Sprint 11 completion docs only after acceptance criteria pass.

## Testing strategy

- Fixture-only HTTP handlers; no live Google/YouTube network calls.
- Adapter tests:
  - readonly scope and PKCE authorization URI,
  - token exchange stores access/refresh tokens only in `ISecretStore`,
  - playlist pagination,
  - playlist item pagination,
  - public/private/deleted/unavailable video mapping,
  - duration parsing from `videos.list`,
  - 401 refresh success and refresh failure/revoke,
  - 403 quota/policy error message,
  - 429 `Retry-After`.
- Server tests:
  - OAuth start/complete/list/import through `/api/v1`,
  - API responses never expose access/refresh tokens or secret references,
  - Mirror sync remains idempotent and preserves local resolution status through existing store behavior.
- Policy tests:
  - YouTube provider public members do not expose play, stream, media URL or download methods.
  - Desktop/provider cards do not expose YouTube playback/download actions.
- Required validation:
  - `dotnet restore`
  - `dotnet build -c Release`
  - `dotnet test -c Release --no-build`
  - `git diff --check`
  - forbidden provider-audio symbol scan.

## Migration and rollback

- No database migration is expected.
- Rollback is removing YouTube provider registration and leaving imported local playlist snapshots intact.
- Any stored YouTube credentials must be removable through existing disconnect flow.

## Security, privacy and license impact

- YouTube tokens must stay inside `ISecretStore`; SQLite may contain only opaque secret references.
- Do not log access tokens, refresh tokens, OAuth codes, code verifiers, Authorization headers or private playlist URLs.
- YouTube is a metadata/playlist source only.
- Do not add yt-dlp, iframe playback, video/audio media URLs, background playback or extraction libraries.
- AGPL-3.0 posture is unchanged.

## Risks and stop conditions

- Stop and request a decision if YouTube support appears to require audio extraction, an iframe/video player, background playback, a cloud secret store or a broader Google scope.
- Stop before introducing a public API status rename from `AuthorizationExpired` to `ReauthorizationRequired` unless the compatibility impact is accepted.
- Treat token leakage into SQLite, logs, API DTOs or Desktop state as a release blocker.
- Treat private/deleted item fixture uncertainty as a reason to preserve conservative metadata rather than dropping rows.

## Acceptance-criteria mapping

- User imports YouTube playlists as local playlists: covered by fixture-backed adapter and server import tests.
- Private/deleted items remain visible with clear status and no crash: covered by playlist item mapping tests.
- No YouTube audio/download/background playback code: covered by provider public API tests, Desktop action tests and forbidden symbol scans.
- Token expiry and revoke lead account into reauthorization-required state: covered by 401 refresh failure/revoke tests mapped to existing `AuthorizationExpired` account state unless the API status name is explicitly changed.
