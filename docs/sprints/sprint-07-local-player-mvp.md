## Sprint 7 - Lokalni player MVP

## Status

Planned

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

Report changed files, validation commands and results, migrations, security/license impact, known risks and every unmet acceptance criterion.
