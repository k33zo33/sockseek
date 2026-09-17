## Sprint 4 - Avalonia desktop shell i daemon supervisor

## Status

Completed

## Required context

Always read `/AGENTS.md` and `/docs/project-state.yaml`, then:

- [ARCHITECTURE.md](../ARCHITECTURE.md)
- [UI_UX.md](../UI_UX.md)
- [application-api.md](../application-api.md)

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

### Changed files

- `Sockseek.Desktop/DesktopShellWindowViewModel.cs` — preserved the last valid backend summary in disconnected states so the UI does not falsely report a healthy connection while the daemon is restarting or offline.
- `Sockseek.Desktop/ShellNavigationViewModel.cs` — kept handshake state only when the supervisor is connected, clearing stale session state on restarting/disconnected transitions.
- `Sockseek.Desktop/SystemDesktopProcessLauncher.cs` — normalized Windows-safe process invocation, working-directory handling and stdout/stderr trimming so the local daemon startup handshake is reliably parsed across platforms.
- `Sockseek.Desktop.Tests/DesktopShellWindowViewModelTests.cs` — regression coverage for stale backend summary state.
- `Sockseek.Desktop.Tests/SystemDesktopProcessLauncherTests.cs` — regression coverage for Windows-compatible output handling.
- `docs/project-state.yaml` — sprint status closed after acceptance criteria passed.
- `docs/sprints/sprint-04-avalonia-desktop-shell.md` — completion report recorded.

### Validation commands and results

Executed:

```bash
dotnet test g:\REPO\Sockseek\sockseek\Sockseek.Desktop.Tests\Sockseek.Desktop.Tests.csproj -c Release --no-restore -v minimal
```

Result:

- 161 total tests
- 161 succeeded
- 0 failed
- 0 skipped
- build succeeded

### Migrations

- No database migrations were introduced in Sprint 4.
- No persistence schema changes were required.

### Security, privacy and license impact

- No new provider audio capability was added.
- Local daemon session token handling remains loopback-only and follows the existing secure handshake design.
- No secrets were logged or exposed in new output paths.
- License remains AGPL-3.0 unchanged.

### Known risks

- Desktop startup remains dependent on local dev-daemon availability and the loopback-only supervisor model.
- Flaky external environment issues can still affect startup timing under constrained CI runners, so the real integration tests rely on bounded waits and the existing handshake contract.

### Unmet acceptance criteria

- None. All Sprint 4 acceptance criteria are satisfied by the validated desktop shell and daemon supervisor behavior.

### Sprint outcome

The desktop shell now auto-starts and supervises the local daemon, recovers through authenticated handshake rotation, and exposes the expected restart/disconnect UX without direct references to Sockseek.Core or EF DbContext.
