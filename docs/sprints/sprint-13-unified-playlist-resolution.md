## Sprint 13 - Unified playlist resolution i bulk workflow

## Status

Complete

## Required context

Always read `/AGENTS.md` and `/docs/project-state.yaml`, then:

- [PROVIDERS.md](../PROVIDERS.md)
- [PLAYER.md](../PLAYER.md)
- [DOMAIN_MODEL.md](../DOMAIN_MODEL.md)
- [UI_UX.md](../UI_UX.md)

## Scope rule

Do not implement future sprint scope. Stop and request an ADR if a locked decision must change.

> **Cilj sprinta**  
> Spojiti provider import, lokalnu biblioteku, Soulseek search, download i player u jedan user-friendly playlist tok.

Ovisnosti: Sprintovi 5-12.

### Isporučivi rezultati

- Playlist resolution orchestrator.

- Bulk resolve/download/play UI.

- Review queue za nejasne local/Soulseek matchove.

- Trajni workflow recovery nakon restarta.

### Implementacijski zadaci

1. Za svaku stavku prvo provjeriti manual mapping, exact IDs i local library.

1. Batchati Soulseek submissione uz postojeće concurrency limite.

1. Povezati application PlaylistItem status s Core workflow/job snapshotovima.

1. Implementirati bulk pause/cancel/retry bez rušenja uspješnih stavki.

1. Implementirati Play available i Play from here; unresolved stavka može pokrenuti resolve-and-play workflow.

1. Sačuvati korisničke candidate odluke za budući sync.

1. Dodati summary: available, downloading, review, failed i skipped.

### Acceptance kriteriji

- Spotify/YouTube/Bandcamp imported playlista može postati potpuno lokalno reproducibilna kroz biblioteku i Soulseek.

- Restart tijekom bulk downloada ne gubi trajne rezultate; aktivni engine posao se korektno rehidrira ili označi za retry.

- User review odluka ostaje nakon provider synca.

- Play nikad ne kontaktira provider audio servis.

- Partial success je jasno prikazan i moguće ga je nastaviti.

### Obavezni testovi

- End-to-end imported playlist fixtures.

- Restart/recovery tests.

- Bulk cancel/retry tests.

- Manual review persistence tests.

- Provider-to-local/Soulseek source resolver tests.

> **Izlazni artefakt sprinta**  
> MVP glavni proizvod: vanjska playlista -> lokalni player/downloader.

## Completion report

Completed on 2026-10-05.

Changed files and areas:

- `Sockseek.Api`: playlist DTOs and API client methods for list/detail, local resolve, bulk download, cancel, retry, play, item download/retry/skip/review/map.
- `Sockseek.Server`: playlist endpoints, download orchestration, workflow sync/recovery, local playback queue integration and review decision persistence.
- `Sockseek.Infrastructure`: playlist projections, local match resolver, provider snapshot preservation and workflow persistence queries.
- `Sockseek.Desktop`: playlist detail workflow, filters, summaries, item actions and selected-item bulk actions.
- Tests across Domain, Infrastructure, Server and Desktop cover provider import fixtures, local matching, bulk download/retry/cancel, restart recovery, manual review persistence, partial success and local-only playback.

Validation:

- `dotnet build -c Release` passed.
- `dotnet test Sockseek.Desktop.Tests\Sockseek.Desktop.Tests.csproj -c Release --no-build --filter DesktopPlaylistsViewModelTests` passed: 6/6.
- `dotnet test -c Release --no-build` passed across all test projects.
- `git diff --check` passed.
- Provider-audio forbidden symbol scan across production projects returned no matches.

Migrations:

- No new Sprint 13 completion migration was required beyond existing playlist/workflow persistence schema.

Security/license impact:

- Playback remains local/progressive Soulseek only.
- No provider audio URL, provider playback or provider download capability was introduced.
- Provider tokens and credentials remain outside playlist DTOs.
- Removed provider playlist rows are surfaced without deleting local audio files.

Known risks:

- Existing NuGet advisory warnings remain for `AngleSharp` and `SQLitePCLRaw.lib.e_sqlite3`; these move into Sprint 14 packaging/security hardening.
- Packaged install, SBOM, third-party notice bundling and release security smoke tests are Sprint 14 scope.

Unmet Sprint 13 acceptance criteria:

- None known after validation.
