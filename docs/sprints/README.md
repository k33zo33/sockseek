# Sprint index

Only one sprint is active at a time. Read the active sprint file and only its referenced context.

| Sprint | File | Status |
| --- | --- | --- |
| 0 | [sprint-00-baseline-upstream-agpl.md](sprint-00-baseline-upstream-agpl.md) | Completed |
| 1 | [sprint-01-architecture-foundation.md](sprint-01-architecture-foundation.md) | Completed |
| 2 | [sprint-02-soulseek-gateway-local-api-security.md](sprint-02-soulseek-gateway-local-api-security.md) | In Progress |
| 3 | [sprint-03-domain-sqlite-persistence.md](sprint-03-domain-sqlite-persistence.md) | Planned |
| 4 | [sprint-04-avalonia-desktop-shell.md](sprint-04-avalonia-desktop-shell.md) | Planned |
| 5 | [sprint-05-soulseek-search-download-ui.md](sprint-05-soulseek-search-download-ui.md) | Planned |
| 6 | [sprint-06-local-library.md](sprint-06-local-library.md) | Planned |
| 7 | [sprint-07-local-player-mvp.md](sprint-07-local-player-mvp.md) | Planned |
| 8 | [sprint-08-play-while-downloading.md](sprint-08-play-while-downloading.md) | Planned |
| 9 | [sprint-09-provider-framework-secret-store.md](sprint-09-provider-framework-secret-store.md) | Planned |
| 10 | [sprint-10-spotify-playlist-import.md](sprint-10-spotify-playlist-import.md) | Planned |
| 11 | [sprint-11-youtube-playlist-import.md](sprint-11-youtube-playlist-import.md) | Planned |
| 12 | [sprint-12-bandcamp-metabrainz.md](sprint-12-bandcamp-metabrainz.md) | Planned |
| 13 | [sprint-13-unified-playlist-resolution.md](sprint-13-unified-playlist-resolution.md) | Planned |
| 14 | [sprint-14-packaging-legal-security.md](sprint-14-packaging-legal-security.md) | Planned |
| 15 | [sprint-15-performance-compliance-beta.md](sprint-15-performance-compliance-beta.md) | Planned |

## General sprint rules

# 20. Plan razvoja po sprintovima

Plan pretpostavlja sprintove od približno dva tjedna, ali Codex ne smije vezati kvalitetu uz kalendarski rok. Svaki sprint mora završiti mergeabilnim vertikalnim rezultatom, zelenim testovima i dokumentacijom. Sprint se smije podijeliti u više PR-ova, ali ne smije spojiti više budućih sprintova u jedan nekontrolirani refactor.

| Sprint | Tema | MVP milestone |
| --- | --- | --- |
| 0 | Baseline, upstream i AGPL odluke | Reproducibilna osnova |
| 1 | Arhitekturni foundation | Novi slojevi i CI |
| 2 | Soulseek gateway i lokalni API security | Stabilna granica prema engineu |
| 3 | Domain i SQLite persistence | Trajni katalog/playlist model |
| 4 | Avalonia desktop shell | Pokretljiv UI + daemon |
| 5 | Soulseek search/download UI | Prvi end-to-end download |
| 6 | Lokalna library | Indeksirana glazba |
| 7 | Player MVP | Lokalna reprodukcija |
| 8 | Play while downloading | Progressive Soulseek playback |
| 9 | Provider framework i secret store | Sigurne integracije |
| 10 | Spotify playlist import | Prvi account provider |
| 11 | YouTube playlist import | Drugi account provider |
| 12 | Bandcamp + MetaBrainz | URL import i metadata |
| 13 | Unified playlist resolution | Cijela imported playlista -> player/downloader |
| 14 | Packaging, legal i security hardening | Release candidate |
| 15 | Performance, compliance i beta stabilizacija | Javna beta odluka |
