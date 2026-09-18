## Sprint 6 - Lokalna glazbena biblioteka

## Status

Completed

## Required context

Always read `/AGENTS.md` and `/docs/project-state.yaml`, then:

- [DOMAIN_MODEL.md](../DOMAIN_MODEL.md)
- [DATABASE.md](../DATABASE.md)
- [UI_UX.md](../UI_UX.md)

## Scope rule

Do not implement future sprint scope. Stop and request an ADR if a locked decision must change.

> **Cilj sprinta**  
> Indeksirati postojeću glazbu i omogućiti da imported playlist prvo koristi lokalne datoteke.

Ovisnosti: Sprintovi 3-4.

### Isporučivi rezultati

- Library root management.

- Background scan i file watcher.

- Artist/album/track prikazi i lokalna pretraga.

- LocalMediaFile -> CanonicalTrack matching.

### Implementacijski zadaci

1. Implementirati TagLib metadata reader i codec/property extraction.

1. Dodati scan checkpoint i progress evente.

1. Detektirati deleted/moved/modified datoteke.

1. Uvesti optional content hash kao low-priority background posao.

1. Implementirati duplicate grouping.

1. Dodati manual relink i rescan akcije.

### Acceptance kriteriji

- Ponovljeni scan ne duplicira file zapise.

- Promijenjeni tagovi se osvježavaju.

- Obrisana datoteka postaje unavailable bez brisanja track identiteta.

- 10.000 track fixture se pretražuje i prikazuje bez zamrzavanja.

- Playlist item s exact local matchom automatski postaje AvailableLocal.

### Obavezni testovi

- Temp-directory scan integration tests.

- Tag fixture tests.

- Move/delete watcher tests.

- 10k performance benchmark.

> **Izlazni artefakt sprinta**  
> Funkcionalna lokalna biblioteka i source resolver za player.

## Completion report

Completed on 2026-09-18.

Changed areas:

- `Sockseek.Infrastructure`: library roots, TagLib metadata reader, scanner/checkpoints/progress, file watcher, relink, content hashing, duplicate grouping, local search/query store, playlist local matching and playback source resolver.
- `Sockseek.Server`: `/api/v1/library/*` endpoints, library endpoint service, background library watcher hosted service, and playback resolver composition.
- `Sockseek.Desktop`: Library view model and UI surface for roots, scan/search, albums, duplicates and local file metadata.
- `Sockseek.Player`: playback coordinator can resolve local playback sources through the application abstraction.
- `Sockseek.Api`: library DTO/client contracts.
- `docs/plans`: Sprint 6 implementation plans for the local library, API, album browse, playback source resolver and background watcher.

Validation:

- `dotnet restore` passed.
- `dotnet test Sockseek.Server.Tests\Sockseek.Server.Tests.csproj -c Release --no-restore -p:BuildInParallel=false -m:1` passed, 103 tests.
- `dotnet test Sockseek.Architecture.Tests\Sockseek.Architecture.Tests.csproj -c Release --no-restore -p:BuildInParallel=false -m:1` passed, 5 tests.
- `dotnet test Sockseek.Infrastructure.Tests\Sockseek.Infrastructure.Tests.csproj -c Release --no-restore -p:BuildInParallel=false -m:1` passed, 49 tests.
- `dotnet build -c Release --no-restore -p:BuildInParallel=false -m:1` passed.
- `dotnet test -c Release --no-build -p:BuildInParallel=false -m:1` passed.

Migrations:

- Sprint 6 added EF migrations for library roots/search indexing/content hash/album metadata in earlier commits.
- No additional migration was required for the playback resolver or background watcher slices.

Security, privacy and license impact:

- Local-first behavior is preserved; scans and playback resolution use local filesystem records only.
- No Spotify, YouTube, Bandcamp or MusicBrainz audio playback/download capability was introduced.
- No `IPlaybackProvider`, provider audio URL, `GetAudioStreamAsync` or `DownloadTrackAsync` contract was introduced.
- AGPL-3.0 posture is unchanged.

Known risks:

- Existing dependency advisories remain: `AngleSharp` moderate severity and `SQLitePCLRaw.lib.e_sqlite3` high severity.
- Existing Desktop test fake-event CS0067 warnings remain.

Unmet acceptance criteria:

- None known. Sprint 6 acceptance criteria are covered by scanner, query store, playlist match, endpoint, Desktop view model, resolver, watcher and background hosted service tests.
