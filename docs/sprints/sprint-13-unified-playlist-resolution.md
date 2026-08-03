## Sprint 13 - Unified playlist resolution i bulk workflow

## Status

Planned

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

Report changed files, validation commands and results, migrations, security/license impact, known risks and every unmet acceptance criterion.
