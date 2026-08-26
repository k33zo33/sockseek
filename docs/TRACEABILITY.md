# Requirements traceability

## 26. Traceability matrica

| Zahtjev | Primarni sprintovi | Dokaz prihvata |
| --- | --- | --- |
| User-friendly desktop UI | 4, 5, 13 | E2E onboarding/search/playlist flow i UI screenshots. |
| Spotify playlist source | 9, 10 | OAuth/import/sync fixture i live allowlist smoke test. |
| YouTube playlist source | 9, 11 | mine=true import, pagination i deleted item test. |
| Bandcamp source | 12 | Public URL fixture import; no credential test. |
| MusicBrainz metadata | 12 | Rate-limit/cache i ISRC/MBID enrichment test. |
| Soulseek downloader | 2, 5, 13 | Gateway parity i E2E download. |
| Full local player | 7 | Codec matrix i queue persistence. |
| Play while downloading | 8 | Slow-stream/underrun/cancel tests. |
| Local library | 6 | Scan/watcher/10k performance tests. |
| AGPL path A | 0, 14 | LICENSE/About/source/release artifact checklist. |
| Existing fork as base | 0-2 | Baseline tag, upstream sync i adapter parity. |
| No external-service streaming | 0, 9-12, 13 | Provider contracts bez playback/download metoda i architecture tests. |
