## Sprint 3 - Domain model i SQLite persistence

## Status

In Progress

## Required context

Always read `/AGENTS.md` and `/docs/project-state.yaml`, then:

- [DOMAIN_MODEL.md](../DOMAIN_MODEL.md)
- [DATABASE.md](../DATABASE.md)

## Scope rule

Do not implement future sprint scope. Stop and request an ADR if a locked decision must change.

> **Cilj sprinta**  
> Uvesti trajni kanonski katalog, playliste, provider snapshotove, local media zapise i migracije.

Ovisnosti: Sprint 1; može se paralelno završavati sa Sprintom 2 uz odvojene PR-ove.

### Isporučivi rezultati

- Domain agregati i state enumovi.

- EF Core DbContext i početna SQLite migracija.

- Repository/use-case sloj za playliste i canonical tracks.

- Database backup/migration runner.

### Implementacijski zadaci

1. Implementirati tablice iz poglavlja 8.

1. Dodati unique indekse i concurrency tokene.

1. Implementirati idempotentno spremanje ExternalPlaylist snapshotova.

1. Implementirati TrackIdentityService s exact match metodama i konfigurabilnim fuzzy pragovima.

1. Dodati migration backup prije promjene schema verzije.

1. Dodati seed samo za development fixture podatke.

### Acceptance kriteriji

- Ponovljeni import istog snapshot-a ne duplicira podatke.

- Brisanje ExternalAccounta ne briše LocalMediaFile niti CanonicalTrack koji imaju druge veze.

- Migracija iz prazne i prethodne test baze prolazi.

- Token vrijednosti ne postoje u DB schema/modelu.

### Obavezni testovi

- SQLite repository integration tests.

- Migration upgrade/backup tests.

- Track matching fixture tests.

- Concurrency/idempotency tests.

> **Izlazni artefakt sprinta**  
> Trajna lokalna baza spremna za UI, provider import i player queue.

## Completion report

Report changed files, validation commands and results, migrations, security/license impact, known risks and every unmet acceptance criterion.
