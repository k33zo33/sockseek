## Sprint 5 - Soulseek search i download UI

## Status

Completed

## Required context

Always read `/AGENTS.md` and `/docs/project-state.yaml`, then:

- [application-api.md](../application-api.md)
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

Completed in the current working tree.

Changed areas:

- Soulseek retry support across `DownloadEngine`, `EngineSupervisor`, server routes, API client, OpenAPI, and Desktop download queue.
- Desktop search/download UI for explicit candidate downloads, candidate metadata, profile/basic quality filters, bounded result lists, workflow details, notifications, and open file/folder actions.
- Tests for retry, explicit candidate actions, metadata display, quality filter mapping, UI error correlation IDs, bounded result lists, workflow detail loading, notifications, and file/folder open handoff.
- Documentation cleanup for the previous `docs/API.md` / `docs/api.md` case-collision, now split into `docs/application-api.md` and `docs/current-api.md`.

Validation:

- `dotnet build -c Release --no-restore` passed.
- `dotnet test -c Release --no-build` passed:
  - Architecture: 5
  - Application: 3
  - Domain: 26
  - Infrastructure: 15
  - Server: 100
  - CLI: 254
  - Core: 578
  - Desktop: 179
- `git diff --check` passed.

Migrations:

- None.

Security/license impact:

- No provider audio capability was introduced.
- Local file/folder opening uses OS shell handoff and validates local path availability.
- Existing AGPL-3.0 and local-first decisions remain unchanged.

Known risks:

- Large-result UI coverage is structural/VM-level, not a rendered performance trace.
- Build still reports pre-existing package advisory warnings for `AngleSharp` and `SQLitePCLRaw.lib.e_sqlite3`, plus existing fake-event CS0067 warnings in Desktop tests.

Unmet acceptance criteria:

- None identified for Sprint 5.
