## Sprint 11 - YouTube playlist import

## Status

Complete

## Required context

Always read `/AGENTS.md` and `/docs/project-state.yaml`, then:

- [PROVIDERS.md](../PROVIDERS.md)
- [SECURITY.md](../SECURITY.md)

## Scope rule

Do not implement future sprint scope. Stop and request an ADR if a locked decision must change.

> **Cilj sprinta**  
> Povezati Google/YouTube račun i uvesti playlist metadata bez reprodukcije ili preuzimanja YouTube audija.

Ovisnosti: Sprint 9.

### Isporučivi rezultati

- Google installed-app OAuth adapter.

- YouTube playlist i playlistItems import.

- Quota/pagination/error UX.

- Policy guard testovi koji zabranjuju audio funkcije.

### Implementacijski zadaci

1. Koristiti readonly YouTube scope i system browser/loopback redirect.

1. Dohvatiti korisničke playliste autoriziranim mine=true zahtjevom.

1. Dohvatiti sve playlist items s paginationom.

1. Mapirati video ID, title, channel, duration ako je dostupna kroz dodatni batch lookup, thumbnail i URL.

1. Obraditi deleted/private/unavailable video kao unresolved metadata item.

1. Ne dodavati yt-dlp ni iframe/video player u novi UI.

### Acceptance kriteriji

- Korisnik uvozi svoje YouTube playliste kao lokalne playliste.

- Private/deleted stavke ostaju vidljive s jasnim statusom, bez crasha.

- Nema YouTube audio, download ili background playback koda u novim projektima.

- Token expiry i revoke vode account u ReauthorizationRequired.

### Obavezni testovi

- OAuth fixture tests.

- Playlist pagination tests.

- Deleted/private item tests.

- Static architecture/policy test koji zabranjuje download/playback metode u YouTube projektu.

> **Izlazni artefakt sprinta**  
> YouTube je potpuno podržan kao source playlista, bez audio policy rizika u aplikaciji.

## Completion report

Completed in local commits `1e56159` through `56a3a41`.

Changed files and areas:

- YouTube playlist-source adapter:
  - `Sockseek.Integrations.YouTube/*`
  - `Sockseek.Application.Tests/Providers/YouTubePlaylistSourceProviderTests.cs`
  - `Sockseek.Application.Tests/Sockseek.Application.Tests.csproj`
  - `Sockseek.Application.Tests/packages.lock.json`
  - `Sockseek.sln`
- Server/API provider wiring:
  - `Sockseek.Server/Sockseek.Server.csproj`
  - `Sockseek.Server/ServerHost.cs`
  - `Sockseek.Server/ServerOptions.cs`
  - `Sockseek.Server/packages.lock.json`
  - `Sockseek.Server.Tests/ProviderEndpointTests.cs`
  - `Sockseek.Server.Tests/packages.lock.json`
  - `Sockseek.Cli/packages.lock.json`
  - `Sockseek.Cli.Tests/packages.lock.json`
- Documentation:
  - `docs/plans/sprint-11-youtube-playlist-import.md`
  - `docs/sprints/README.md`

Validation commands and results:

- `dotnet restore`: passed with existing NuGet advisory warnings for `AngleSharp` and `SQLitePCLRaw.lib.e_sqlite3`.
- `dotnet build -c Release`: passed with existing NuGet advisory warnings and existing Desktop test fake-event `CS0067` warnings.
- `dotnet test -c Release --no-build`: passed.
  - Architecture: 5 passed.
  - Domain: 26 passed.
  - Application: 42 passed.
  - Player: 34 passed.
  - Core: 578 passed.
  - Infrastructure: 63 passed.
  - CLI: 254 passed.
  - Desktop: 215 passed.
  - Server: 125 passed.
- `git diff --check`: passed.
- Forbidden provider-audio scan for `IPlaybackProvider`, `GetAudioStreamAsync`, `DownloadTrackAsync`, provider audio URLs, `yt-dlp`, iframe playback and background playback terms across production projects: no matches.

Acceptance criteria:

- Users can authorize YouTube with installed-app PKCE, list `mine=true` playlists and import/sync playlists through the existing provider API.
- The adapter uses only `https://www.googleapis.com/auth/youtube.readonly`; playback/download-like scopes are not accepted from callers.
- Playlist and playlist item pagination are covered by local fixture tests.
- Public videos map video ID, title, channel, duration, thumbnail, YouTube external URL and raw metadata into local playlist snapshots.
- Private/deleted/unavailable video items remain visible as metadata rows and do not crash import.
- Mirror imports are idempotent through the existing playlist snapshot store; removed source items are marked locally without deleting local data.
- Access and refresh token values are stored only through `ISecretStore`; API account responses do not expose token values or secret references.
- Token refresh success updates the local secret reference; refresh failure/revoke returns `provider_reauthorization_required` and marks the account `AuthorizationExpired`, the current reauthorization-required state.
- No YouTube player, iframe, background playback, audio URL, extraction or download capability was introduced.

Migrations:

- No EF migration was required. YouTube playlist and item metadata fit the existing external account, external playlist and playlist item snapshot schema.

Security, privacy and license impact:

- YouTube is a playlist/metadata source only.
- OAuth codes, code verifiers, access tokens and refresh tokens are not exposed through API DTOs or stored in SQLite.
- No credentials, cookies, YouTube media URLs, iframe player or extraction libraries were added.
- AGPL-3.0 posture is unchanged.

Known risks and unmet criteria:

- No unmet Sprint 11 acceptance criteria remain.
- Existing dependency advisory warnings remain part of the current baseline: `AngleSharp` moderate and `SQLitePCLRaw.lib.e_sqlite3` high.
- Live Google quota, consent-screen and channel-edge behavior remain operational risks outside fixture tests; the implementation is fixture-first and uses the documented readonly YouTube Data API path.
