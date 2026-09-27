## Sprint 10 - Spotify playlist import

## Status

Complete

## Required context

Always read `/AGENTS.md` and `/docs/project-state.yaml`, then:

- [PROVIDERS.md](../PROVIDERS.md)
- [SECURITY.md](../SECURITY.md)

## Scope rule

Do not implement future sprint scope. Stop and request an ADR if a locked decision must change.

> **Cilj sprinta**  
> Omogućiti allowlistanim korisnicima povezivanje Spotify računa i uvoz/sinkronizaciju playlista bez Spotify playbacka.

Ovisnosti: Sprint 9.

### Isporučivi rezultati

- Spotify PKCE adapter.

- Playlist list/details import i pagination.

- Development-mode/quota UX.

- Spotify provider fixtures i sync testovi.

### Implementacijski zadaci

1. Registrirati minimalne readonly scopeove: playlist-read-private i playlist-read-collaborative; user-library-read samo ako se implementira saved tracks import.

1. Mapirati track, episode/unsupported item i unavailable item slučajeve.

1. Sačuvati ISRC, external ID, URL, artist, album, duration i artwork metadata.

1. Implementirati Copy i Mirror import.

1. Obraditi 401 refresh, 403 not allowlisted, 429 Retry-After i pagination.

1. Dodati “Open in Spotify” samo kao vanjski link, bez play kontrole.

### Acceptance kriteriji

- Allowlistani korisnik vidi i uvozi svoje playliste.

- Neallowlistani 403 ima razumljivu poruku o Spotify development modeu.

- Ponovljeni sync ne duplicira stavke i čuva local resolution status.

- Aplikacija nema Spotify player niti audio endpoint.

- Disconnect briše credential i playlist snapshot ostaje kao lokalna kopija po korisničkoj odluci.

### Obavezni testovi

- Recorded HTTP fixture tests.

- Pagination, 401, 403, 429 tests.

- Mirror sync diff tests.

- UI no-playback assertion test.

> **Izlazni artefakt sprinta**  
> Spotify playlist source funkcionalan u ograničenom beta okruženju.

## Completion report

Completed in local commits `2559f6b` through `b662bfe`.

Changed files and areas:

- Spotify playlist-source adapter:
  - `Sockseek.Integrations.Spotify/*`
  - `Sockseek.Application.Tests/Providers/SpotifyPlaylistSourceProviderTests.cs`
  - `Sockseek.sln`
  - `packages.lock.json`
- Provider OAuth/API/import surface:
  - `Sockseek.Api/Client/SockseekApiClient.cs`
  - `Sockseek.Api/Client/SockseekApiJsonContext.cs`
  - `Sockseek.Api/Contracts/ServerRequests.cs`
  - `Sockseek.Api/Contracts/ServerResponses.cs`
  - `Sockseek.Server/ServerHost.cs`
  - `Sockseek.Server.Tests/ProviderEndpointTests.cs`
  - `docs/openapi.json`
- Playlist snapshot metadata and sync safeguards:
  - `Sockseek.Integrations.Abstractions/ProviderCapabilities.cs`
  - `Sockseek.Infrastructure/Persistence/ExternalPlaylistSnapshotRecord.cs`
  - `Sockseek.Infrastructure/Persistence/ExternalPlaylistSnapshotRecordFactory.cs`
  - `Sockseek.Infrastructure/Persistence/ExternalPlaylistSnapshotStore.cs`
  - `Sockseek.Infrastructure.Tests/Persistence/ExternalPlaylistSnapshotStoreTests.cs`
- Desktop/UI policy guard:
  - `Sockseek.Desktop.Tests/ProviderConnectionCardViewModelTests.cs`
- Compatibility fix discovered during full validation:
  - `Sockseek.Server/EngineStateStore.cs`
- Documentation:
  - `docs/PROVIDERS.md`
  - `docs/plans/sprint-10-spotify-playlist-import.md`

Validation commands and results:

- `dotnet restore`: passed with existing NuGet advisory warnings for `AngleSharp` and `SQLitePCLRaw.lib.e_sqlite3`.
- `dotnet build -c Release`: passed with existing NuGet advisory warnings and existing Desktop test fake-event `CS0067` warnings.
- `dotnet test -c Release --no-build`: passed.
  - Architecture: 5 passed.
  - Domain: 26 passed.
  - Application: 34 passed.
  - Player: 34 passed.
  - Core: 578 passed.
  - Infrastructure: 63 passed.
  - CLI: 254 passed.
  - Desktop: 215 passed.
  - Server: 123 passed.
- `git diff --check`: passed.
- Forbidden provider-audio scan for `IPlaybackProvider`, `GetAudioStreamAsync`, `DownloadTrackAsync`, provider audio URLs and related audio URL terms: no matches.

Acceptance criteria:

- Allowlisted users can authorize Spotify through PKCE, list playlists and import/sync playlists through fixture-backed API tests.
- Spotify uses only `playlist-read-private` and `playlist-read-collaborative`; `streaming` and `user-library-read` are excluded.
- Playlist list/details pagination is covered by recorded/local HTTP fixture tests.
- Track, unavailable track, episode and local/unsupported items are mapped without introducing playback/download behavior.
- ISRC, external track ID, external URL, artist, album, duration, artwork and raw metadata are preserved in local playlist snapshots.
- Copy and Mirror imports are supported by the server import endpoint and persistence store.
- Repeated Mirror sync does not duplicate items and preserves local `CanonicalTrackId` plus `AvailableLocal` resolution status.
- Missing provider items in Mirror mode are marked `RemovedFromSourcePlaylist`; local media/download data is preserved.
- Spotify 401 refresh, 403 development-mode/allowlist errors and 429 `Retry-After` handling are covered by fixture tests; the API returns a user-facing 403 message for development-mode rejection.
- Disconnect deletes the stored credential through `ISecretStore`, clears the account secret reference/status and leaves playlist snapshots/local data intact.
- Spotify external URLs are retained only as metadata/open-in-provider links. No Spotify player, audio stream, audio URL or provider download endpoint was introduced.
- Desktop provider action tests assert that provider cards do not expose playback, streaming or provider-track download actions.

Migrations:

- No EF migration was required. Sprint 10 stores additional provider item metadata in the existing playlist item `SnapshotJson` field.

Security, privacy and license impact:

- Access and refresh tokens are stored only through `ISecretStore`; SQLite stores opaque secret references only.
- API account responses do not expose secret references or token values.
- OAuth code verifiers are passed only to token exchange and are not exposed through API response DTOs.
- Spotify remains a playlist/metadata source only; all audio policy decisions remain unchanged.
- AGPL-3.0 posture is unchanged.

Known risks and unmet criteria:

- No unmet Sprint 10 acceptance criteria remain.
- Existing dependency advisory warnings remain part of the current baseline: `AngleSharp` moderate and `SQLitePCLRaw.lib.e_sqlite3` high.
- Spotify live production quota/app-review behavior remains an operational risk outside fixture tests; the app currently targets the documented limited beta/development-mode workflow.
