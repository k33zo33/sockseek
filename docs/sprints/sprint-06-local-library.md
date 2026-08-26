## Sprint 6 - Lokalna glazbena biblioteka

## Status

Planned

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

Report changed files, validation commands and results, migrations, security/license impact, known risks and every unmet acceptance criterion.
