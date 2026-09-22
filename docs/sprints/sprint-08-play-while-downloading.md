## Sprint 8 - Play while downloading

## Status

Completed

## Required context

Always read `/AGENTS.md` and `/docs/project-state.yaml`, then:

- [PLAYER.md](../PLAYER.md)
- [application-api.md](../application-api.md)

## Scope rule

Do not implement future sprint scope. Stop and request an ADR if a locked decision must change.

> **Cilj sprinta**  
> Omogućiti kontroliranu reprodukciju djelomično preuzete Soulseek datoteke kada format i buffer to dopuštaju.

Ovisnosti: Sprintovi 5 i 7.

### Isporučivi rezultati

- ProgressiveMediaSource i buffer state.

- Codec capability matrix s feature flagom.

- Buffering UI i seek ograničenja.

- Fallback na playback nakon kompletnog downloada.

### Implementacijski zadaci

1. Povezati download progress s procjenom playable buffera.

1. Testirati growing-file ponašanje odabranog media enginea.

1. Implementirati početni buffer threshold.

1. Implementirati underrun i resume state.

1. Ograničiti seek na buffered range.

1. Obraditi candidate switch, cancel i failed incomplete file.

### Acceptance kriteriji

- Podržani MP3 fixture počinje svirati prije završetka downloada.

- Prespor download ulazi u Buffering i nastavlja bez corruptanja queuea.

- Ne podržani format čeka complete.

- Cancel zaustavlja playback i čisti privremeni source.

- Nema indeksiranja incomplete filea kao konačne library stavke.

### Obavezni testovi

- Controlled slow-stream tests.

- Underrun/resume tests.

- Cancel/switch candidate tests.

- Codec-specific regression tests.

> **Izlazni artefakt sprinta**  
> Eksperimentalni, feature-flagged “Play while downloading” s jasnim fallbackom.

## Completion report

Completed on 2026-09-22.

Changed areas:

- `Sockseek.Player`: progressive media source and buffer models, deterministic buffer policy, coordinator buffering/underrun/resume behavior, seek limit handling and cancel/switch cleanup.
- `Sockseek.Server`: experimental progressive playback feature flag, MP3-only progressive capability reporting, active Soulseek download snapshot mapping and `/api/v1/player/play/download-job`.
- `Sockseek.Api`: progressive buffer DTO exposure, typed play-download-job request/client method and regenerated OpenAPI.
- `Sockseek.Desktop`: player bar buffer status and download queue Play action for active download jobs.
- `Sockseek.Infrastructure`: `.incomplete` local library scan/watch exclusion hardening.
- `docs`: Sprint 8 execution plan and generated `docs/openapi.json`.

Validation:

- `dotnet test Sockseek.Player.Tests\Sockseek.Player.Tests.csproj -c Release` passed, 34 tests.
- `dotnet test Sockseek.Server.Tests\Sockseek.Server.Tests.csproj -c Release` passed, 114 tests.
- `dotnet test Sockseek.Desktop.Tests\Sockseek.Desktop.Tests.csproj -c Release` passed, 209 tests.
- `dotnet build -c Release` passed.
- `dotnet test -c Release --no-build` passed: Architecture 5, Application 3, Domain 26, Player 34, Infrastructure 56, Core 578, CLI 254, Server 114 and Desktop 209 tests.
- Provider-audio guard search for `IPlaybackProvider`, `GetAudioStreamAsync`, `DownloadTrackAsync`, `AudioUrl` and `audio URL` in application/integration/player/server/desktop projects returned no matches.

Migrations:

- No EF migration was required in Sprint 8.

Security, privacy and license impact:

- Progressive playback opens only local Soulseek `.incomplete` files created by the downloader.
- The feature is experimental and disabled by default; MP3 is the only progressive-safe codec advertised when enabled.
- No Spotify, YouTube, Bandcamp, MusicBrainz or other provider audio playback/download capability was introduced.
- No `IPlaybackProvider`, provider audio URL, `GetAudioStreamAsync` or `DownloadTrackAsync` contract was introduced.
- AGPL-3.0 posture is unchanged.

Known risks:

- Existing dependency advisories remain: `AngleSharp` moderate severity and `SQLitePCLRaw.lib.e_sqlite3` high severity.
- Existing Desktop test fake-event CS0067 warnings remain.
- Progressive playback is still feature-flagged; broader codec support must remain disabled until each codec has growing-file evidence.

Unmet acceptance criteria:

- None known. Sprint 8 acceptance criteria are covered by growing MP3 fixture, buffer policy/coordinator slow-stream and underrun/resume tests, unsupported-format complete fallback tests, cancel/switch cleanup tests, server active-download endpoint coverage, Desktop play action coverage and `.incomplete` scanner/watch exclusion tests.
