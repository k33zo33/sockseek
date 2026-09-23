# Sprint 10 Spotify playlist import

## Goal

Implement the Spotify playlist-source adapter on top of the Sprint 9 provider foundation so allowlisted users can authorize with PKCE, list playlists and import/sync playlist items without introducing Spotify playback, audio URLs or download behavior.

## Current-state findings

- Sprint 9 provides `IPlaylistSourceProvider`, provider capabilities, `ISecretStore`, PKCE/state coordination, a loopback callback listener, provider HTTP retry/backoff primitives and account status UI/API.
- `ProviderIds.Spotify` is already present and default capabilities expose account connection plus playlist import.
- `ExternalTrackSnapshot` already carries the metadata Sprint 10 needs: provider/external IDs, provider item ID, title, artists, album, duration, ISRC, external URL, artwork URL, MBID and raw metadata JSON.
- No Spotify-specific adapter project exists yet.
- Existing provider tests use MSTest and local fake/recording HTTP handlers; no live network credentials should be needed.

## In scope

- Add a `Sockseek.Integrations.Spotify` project with a Spotify playlist-source provider and testable HTTP client boundary.
- Implement Spotify Authorization Code with PKCE start/callback request shaping without storing token values outside `ISecretStore`.
- Support playlist listing and playlist item pagination using recorded/local JSON fixtures.
- Map track items, unavailable tracks and unsupported episode/local items into stable playlist snapshots.
- Handle 401, 403 development-mode/not-allowlisted responses, 429 `Retry-After` and general Spotify API error payloads with user-understandable exceptions.
- Preserve the playlist-only contract: external URLs may be stored as metadata/open-in-browser links, never as audio sources.

## Out of scope

- Spotify playback, preview playback, audio streaming, download, provider media URLs or `IPlaybackProvider`.
- Real live Spotify credential setup in tests.
- YouTube, Bandcamp, MusicBrainz or ListenBrainz implementation.
- Write-back to Spotify playlists.
- Broad playlist resolution/download UI beyond preserving imported item identity and metadata.

## Files and projects affected

- `Sockseek.Integrations.Spotify`: new adapter project, Spotify DTOs/client helpers and provider implementation.
- `Sockseek.Application.Tests` or a new integration-specific test project: recorded fixture tests for pagination, error handling and mapping.
- `Sockseek.sln` and lock files if a new project is added.
- `docs/openapi.json` only if server contracts change; initial adapter-only slices should not change OpenAPI.
- Desktop/API only if Sprint 10 UI affordances or no-playback assertions require them.

## API, schema and event changes

- No database schema change is expected for the first adapter slices.
- No `/api/v1` contract change is required until the server wires real provider authorization/import endpoints.
- If later Sprint 10 slices expose Spotify import endpoints, update `docs/openapi.json` in the same commit and keep responses token-free.

## Implementation sequence

1. Add Spotify integration project and an adapter plan/test fixture scaffold.
2. Implement authorization URI construction with minimal readonly scopes: `playlist-read-private` and `playlist-read-collaborative`; defer `user-library-read` unless saved tracks import is implemented.
3. Implement token completion through `ISecretStore` using fixture-backed token responses.
4. Implement playlist list pagination and map summaries.
5. Implement playlist item pagination and mapping for normal tracks, unavailable tracks, local/unsupported items and episodes.
6. Add error handling tests for 401 refresh-needed/expired, 403 development mode, 429 retry-after and paginated success.
7. Add no-playback assertions scanning provider contracts/UI for Spotify playback/audio/download affordances.
8. Wire provider registration/API/UI only after the adapter foundation is stable and tested.

## Testing strategy

- Recorded/local JSON fixture tests for token exchange, playlist pagination and playlist item pagination.
- Unit tests for mapping track, unavailable, episode and local/unsupported item cases.
- Error tests for 401, 403 and 429 handling without live network.
- Existing full validation:
  - `dotnet restore`
  - `dotnet build -c Release`
  - `dotnet test -c Release --no-build`
- Forbidden provider-audio symbol scan remains mandatory.

## Migration and rollback

- No migration expected initially.
- Rollback is removing Spotify provider registration while leaving imported local playlist snapshots and secret-store references untouched.
- Disconnect must delete Spotify credentials through `ISecretStore`; local playlist snapshots remain unless a later explicit user action deletes them.

## Security, privacy and license impact

- Store access/refresh tokens only via `ISecretStore`; SQLite stores only opaque references.
- Do not log OAuth codes, code verifiers, tokens, client secrets or full Authorization headers.
- Keep Spotify as a playlist/metadata source only.
- AGPL-3.0 posture is unchanged.

## Risks and stop conditions

- Stop and request an ADR if Spotify implementation requires playback, audio URLs, downloads, media extraction or a cloud secret store.
- Stop before adding broad scopes beyond Sprint 10 requirements.
- Treat token leakage into SQLite, logs, API responses or Desktop state as a release blocker.
- Spotify live API terms/quota constraints require development-mode UX and fixture-first tests.

## Acceptance-criteria mapping

- Allowlisted user sees/imports playlists: covered by fixture-backed list/import tests and later server/UI wiring.
- Non-allowlisted 403 message: covered by Spotify API error test for development mode.
- Repeated sync avoids duplicate items and preserves local resolution status: covered by mirror sync tests once persistence import wiring exists.
- No Spotify player/audio endpoint: covered by contract/UI no-playback assertion tests and forbidden symbol scan.
- Disconnect deletes credential while playlist snapshot remains local: covered by Sprint 9 account lifecycle plus Spotify-specific disconnect/import tests.
