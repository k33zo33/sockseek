## Sprint 4 - Avalonia desktop shell i daemon supervisor

## Status

In Progress

## Required context

Always read `/AGENTS.md` and `/docs/project-state.yaml`, then:

- [ARCHITECTURE.md](../ARCHITECTURE.md)
- [UI_UX.md](../UI_UX.md)
- [API.md](../API.md)

## Scope rule

Do not implement future sprint scope. Stop and request an ADR if a locked decision must change.

> **Cilj sprinta**  
> Napraviti user-friendly desktop okvir koji automatski pokreće lokalni daemon i prikazuje njegovo stanje.

Ovisnosti: Sprintovi 1-2.

### Isporučivi rezultati

- Avalonia app shell, sidebar, routing i design tokeni.

- DesktopDaemonSupervisor i secure session handshake.

- Home, Search, Playlists, Library, Downloads, Accounts i Settings prazne stranice.

- Persistent bottom player placeholder.

### Implementacijski zadaci

1. Implementirati single-instance desktop proces.

1. Pokrenuti self-contained ili development daemon child process.

1. Čitati port/token preko sigurnog startup handshaka.

1. Implementirati API client i SignalR reconnect manager.

1. Uvesti theme, localization-ready resources i command palette skeleton.

1. Implementirati backend starting/restarting/disconnected UX.

### Acceptance kriteriji

- Korisnik ne mora ručno pokretati daemon.

- UI se oporavlja nakon kontroliranog restarta daemona.

- Desktop nema referencu na Sockseek.Core ni DbContext.

- Sve glavne stranice i navigation shortcuts rade.

- Light/dark tema se pamti.

### Obavezni testovi

- ViewModel unit tests.

- Headless navigation tests.

- Daemon start/restart integration test.

- Session token handshake test.

> **Izlazni artefakt sprinta**  
> Instalabilni development shell povezan sa stvarnim lokalnim daemonom.

## Completion report

Report changed files, validation commands and results, migrations, security/license impact, known risks and every unmet acceptance criterion.
