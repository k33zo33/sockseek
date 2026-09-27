## Sprint 12 - Bandcamp public URL i MetaBrainz metadata

## Status

Completed

## Required context

Always read `/AGENTS.md` and `/docs/project-state.yaml`, then:

- [PROVIDERS.md](../PROVIDERS.md)
- [DOMAIN_MODEL.md](../DOMAIN_MODEL.md)

## Scope rule

Do not implement future sprint scope. Stop and request an ADR if a locked decision must change.

> **Cilj sprinta**  
> Dodati službeno realan Bandcamp import put i MusicBrainz enrichment; jasno razdvojiti playlist source od metadata providera.

Ovisnosti: Sprintovi 3 i 9.

### Isporučivi rezultati

- Bandcamp public URL importer iza nestabilnosti guardova.

- MusicBrainz API client s limiterom i cacheom.

- MBID/ISRC enrichment queue.

- Opcionalni ListenBrainz ADR/prototype za korisničke playliste.

### Implementacijski zadaci

1. Bandcamp: validirati URL, dohvatiti javni album/track metadata i mapirati tracklistu; ne koristiti login/cookies.

1. Bandcamp parser izolirati iza adaptera i fixture HTML/JSON testova.

1. MusicBrainz: smislen User-Agent, globalni 1 req/s limiter, retry 503 i local cache.

1. Implementirati recording lookup po ISRC-u i fuzzy search samo kao enrichment, ne autoritativni auto-match bez scorea.

1. UI za MusicBrainz ne prikazuje “Connect account” za playlist import.

1. Napraviti ADR hoće li se ListenBrainz uključiti za MetaBrainz korisničke playliste u post-MVP fazi.

### Acceptance kriteriji

- Javni Bandcamp album URL postaje lokalna playlista.

- Promjena parsera ne ruši ostale providere; greška je lokalizirana na import.

- Nema Bandcamp credentials/cookies pohranjenih u aplikaciji.

- MusicBrainz nikad ne prelazi limiter u testiranom scheduleru.

- MBID/ISRC se spremaju i koriste u TrackIdentityServiceu.

### Obavezni testovi

- Bandcamp fixture parser tests.

- MusicBrainz limiter/cache tests.

- ISRC/MBID enrichment tests.

- 503/backoff tests.

> **Izlazni artefakt sprinta**  
> Bandcamp URL import i stabilan metadata enrichment bez lažnih account mogućnosti.

## Completion report

Completed in local commits:

- `04f0be2` - playlist local matching now passes snapshot ISRC and MusicBrainz Recording MBID into `TrackIdentityService`.
- `6d491bf` - Bandcamp public album/track URL parser and fixture tests.
- `a593d3e` - `/api/v1/providers/{providerId}/public-playlists/import` API wiring for Bandcamp public URLs.
- `93da793` - MusicBrainz metadata client with User-Agent, global limiter, cache, ISRC lookup, conservative fuzzy search and 503 retry/backoff tests.
- `2903c00` - MusicBrainz enrichment queue/store persists MBID/ISRC and TrackSource metadata, then local matching uses it.
- `08ffa1f` - ListenBrainz post-MVP ADR and MusicBrainz UI no-connect guard.
- `169e108` - fixed a server gateway test cleanup race exposed by full validation.

Changed areas:

- `Sockseek.Integrations.Bandcamp`
- `Sockseek.Integrations.MusicBrainz`
- `Sockseek.Api`, `Sockseek.Server`, `docs/openapi.json`
- `Sockseek.Infrastructure` persistence enrichment queue/store
- `Sockseek.Application.Tests`, `Sockseek.Infrastructure.Tests`, `Sockseek.Server.Tests`, `Sockseek.Desktop.Tests`
- `docs/adr/0008-listenbrainz-post-mvp.md`

Validation:

- `dotnet restore` - passed; existing NuGet advisory warnings remain for AngleSharp and SQLitePCLRaw.
- `dotnet build -c Release` - passed; existing NuGet advisory warnings and Desktop test fake-event CS0067 warnings remain.
- `dotnet test -c Release --no-build` - passed after `169e108`; project totals: Architecture 5, Application 53, Domain 26, Player 34, Core 578, Infrastructure 67, CLI 254, Desktop 216, Server 126.
- `git diff --check` - passed.
- Forbidden provider-audio symbol scan across production projects - passed.

Targeted tests added or updated:

- Bandcamp fixture parser/import tests.
- Bandcamp server/API public URL import test.
- MusicBrainz limiter/cache, ISRC lookup, fuzzy score and 503/backoff tests.
- ISRC/MBID enrichment persistence and playlist local match tests.
- MusicBrainz Desktop capability test proving no Connect account action.

Migrations:

- No EF migration. Existing `CanonicalTracks.Isrc`, `CanonicalTracks.MusicBrainzRecordingId` and `TrackSources` columns were sufficient.

Security, privacy and license impact:

- No Bandcamp credentials, cookies or authenticated scraping were added.
- MusicBrainz remains metadata lookup/enrichment only.
- ListenBrainz account/history support is explicitly deferred by ADR-0008.
- No provider playback, provider audio URL or provider download capability was introduced.
- AGPL-3.0 posture unchanged.

Known risks:

- Bandcamp parsing is fixture-backed and localized to public album/track import. If Bandcamp changes public page structured metadata, import fails locally with a provider error instead of affecting other providers.

Unmet acceptance criteria:

- None.
