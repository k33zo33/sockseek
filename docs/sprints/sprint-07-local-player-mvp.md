## Sprint 7 - Lokalni player MVP

## Status

Completed

## Required context

Always read `/AGENTS.md` and `/docs/project-state.yaml`, then:

- [PLAYER.md](../PLAYER.md)
- [UI_UX.md](../UI_UX.md)
- [DATABASE.md](../DATABASE.md)

## Scope rule

Do not implement future sprint scope. Stop and request an ADR if a locked decision must change.

> **Cilj sprinta**  
> Reproducirati lokalne i dovršene Soulseek datoteke kroz stabilan player i trajni queue.

Ovisnosti: Sprint 6; Sprint 4 UI shell.

### Isporučivi rezultati

- IMediaEngine adapter i PlaybackCoordinator.

- Bottom player i expanded queue ekran.

- Trajni queue i media key podrška.

- Codec capability report.

### Implementacijski zadaci

1. Napraviti spike i odabrati LibVLCSharp ili drugi lokalni engine kroz ADR.

1. Implementirati player state machine.

1. Dodati queue persistence i deterministic shuffle.

1. Dodati seek, volume, repeat i error handling.

1. Dodati OS media session bridge po platformi.

1. Čitati cover art i now-playing metadata iz lokalne datoteke.

### Acceptance kriteriji

- MP3, FLAC, Ogg, Opus, WAV i M4A fixture matrica ima dokumentirani rezultat.

- Queue se vraća nakon restarta.

- Jedan neispravan file ne ruši cijeli player.

- Media keys rade na Windows targetu.

- Player nikad ne pokušava provider audio URL.

### Obavezni testovi

- Player state unit tests.

- Codec fixture integration tests.

- Queue persistence tests.

- Long playback smoke test.

> **Izlazni artefakt sprinta**  
> Aplikacija je pun lokalni player za library i dovršene downloade.

## Completion report

Completed on 2026-09-21.

Changed areas:

- `Sockseek.Player`: playback state machine, queue navigation, local LibVLC media engine, codec capability report and bad-file recovery coverage.
- `Sockseek.Server`: local player control API, persisted queue restore/save, now-playing metadata/artwork resolution, OpenAPI-safe hosted-service startup and database parent-directory creation.
- `Sockseek.Infrastructure`: local playback source resolution foundation and local embedded artwork extraction/cache helper.
- `Sockseek.Desktop`: bottom player binding, expanded queue panel, focused key routing and Windows media key bridge.
- `Sockseek.Api`: typed player client methods and player capability/now-playing contracts.
- `docs`: LibVLC ADR, codec fixture matrix and Sprint 7 execution plans.

Validation:

- `dotnet test Sockseek.Infrastructure.Tests\Sockseek.Infrastructure.Tests.csproj -c Release` passed, 54 tests.
- `dotnet test Sockseek.Server.Tests\Sockseek.Server.Tests.csproj -c Release` passed, 107 tests.
- `dotnet build -c Release` passed.
- `dotnet test -c Release --no-build` passed: Architecture 5, Application 3, Domain 26, Player 22, Infrastructure 54, Core 578, CLI 254, Server 107 and Desktop 206 tests.
- Provider-audio guard search for `IPlaybackProvider`, `GetAudioStreamAsync`, `DownloadTrackAsync`, `AudioUrl` and `audio URL` in application/integration/player/server/desktop projects returned no matches.

Migrations:

- No EF migration was required in Sprint 7.
- Queue persistence tables already existed from the accepted persistence schema work; artwork cache files are derived local artifacts and are not stored in SQLite.

Security, privacy and license impact:

- Playback and artwork use only local files or local cache paths.
- No Spotify, YouTube, Bandcamp, MusicBrainz or other provider audio playback/download capability was introduced.
- No `IPlaybackProvider`, provider audio URL, `GetAudioStreamAsync` or `DownloadTrackAsync` contract was introduced.
- AGPL-3.0 posture is unchanged.

Known risks:

- Existing dependency advisories remain: `AngleSharp` moderate severity and `SQLitePCLRaw.lib.e_sqlite3` high severity.
- Existing Desktop test fake-event CS0067 warnings remain.
- Progressive play-while-downloading remains Sprint 8 scope.

Unmet acceptance criteria:

- None known. Sprint 7 acceptance criteria are covered by player state, codec fixture, queue persistence/restore, bad-file recovery, Windows media key, now-playing artwork and provider-audio guard validation.
