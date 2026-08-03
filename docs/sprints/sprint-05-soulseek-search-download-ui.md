## Sprint 5 - Soulseek search i download UI

## Status

Planned

## Required context

Always read `/AGENTS.md` and `/docs/project-state.yaml`, then:

- [API.md](../API.md)
- [UI_UX.md](../UI_UX.md)
- [ARCHITECTURE.md](../ARCHITECTURE.md)

## Scope rule

Do not implement future sprint scope. Stop and request an ADR if a locked decision must change.

> **Cilj sprinta**  
> Isporučiti prvi puni vertikalni tok: search -> candidate -> download -> otvorena lokalna datoteka.

Ovisnosti: Sprintovi 2 i 4.

### Isporučivi rezultati

- Search ekran s track/album modom.

- Candidate list i album folder pregled.

- Download queue ekran i notifications.

- Cancel, retry i next-candidate akcije.

### Implementacijski zadaci

1. Mapirati quality profile i osnovne filtere u search request.

1. Virtualizirati veliki result list.

1. Prikazati user, slot, speed, format, bitrate, sample rate, bit depth i trajanje gdje postoje.

1. Dodati candidate review i explicit download action.

1. Prikazati workflow tree/detail drawer.

1. Dodati “Open file/folder” nakon uspjeha.

### Acceptance kriteriji

- Korisnik može pretražiti i preuzeti pojedinačnu pjesmu bez CLI-ja.

- Progress se ažurira bez blokiranja UI-ja.

- Cancel i next candidate mijenjaju stvarni engine posao.

- Greške imaju retry i correlation ID.

- Veliki result list ne zamrzava UI.

### Obavezni testovi

- Mock Soulseek E2E search/download.

- Large result virtualization test.

- Disconnect/reconnect tijekom downloada.

- UI error-state tests.

> **Izlazni artefakt sprinta**  
> Prva korisna desktop verzija koja zamjenjuje CLI za pojedinačni download.

## Completion report

Report changed files, validation commands and results, migrations, security/license impact, known risks and every unmet acceptance criterion.
