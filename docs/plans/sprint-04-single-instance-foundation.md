# Sprint 4 single-instance foundation

## Goal

Add the smallest safe desktop-side single-instance foundation so the shell can refuse a second launch before later Avalonia bootstrap work lands.

## Current-state findings

- `Sockseek.Desktop` currently has a placeholder `Program.Main` that ignores arguments and does not guard against duplicate launches.
- Sprint 4 explicitly requires a single-instance desktop process.
- The daemon supervisor, API client, reconnect manager, shell navigation, and theme/palette foundations already exist, so single-instance gating is now one of the main missing shell deliverables.
- There is no existing desktop bootstrap abstraction to test process-startup behavior without invoking a real UI.

## In scope

- Add a desktop single-instance gate abstraction and a named-mutex implementation.
- Add a small program runner that acquires the gate before invoking the rest of desktop startup.
- Update `Program` to use the new runner.
- Add unit tests for first-instance success, duplicate-instance rejection, and lease disposal.

## Out of scope

- Foregrounding or message handoff to an existing window.
- Named-pipe or socket IPC between desktop instances.
- Full Avalonia app bootstrap.
- Installer or OS shell integration.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-single-instance-foundation.md`

## API, schema and event changes

- No server API changes.
- No schema or migration changes.
- Desktop-only startup abstraction changes.

## Implementation sequence

1. Add single-instance gate and lease abstractions.
2. Implement named-mutex desktop gate.
3. Add a program runner to make startup behavior unit-testable.
4. Update `Program.Main` to run through the gate.
5. Add unit tests and run targeted desktop tests.

## Testing strategy

- Add focused unit tests in `Sockseek.Desktop.Tests` for startup gating behavior.
- Run the desktop test project in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No secret handling changes.
- No network behavior changes.
- No license impact.

## Risks and stop conditions

- Stop if real startup wiring requires Avalonia composition not yet present in Sprint 4’s current codebase.
- Stop if a cross-platform mutex approach proves unreliable in the supported .NET target environment.

## Acceptance-criteria mapping

- Directly contributes to “Implementirati single-instance desktop proces”.
- Supports the shell deliverable without introducing future-sprint UI scope.
