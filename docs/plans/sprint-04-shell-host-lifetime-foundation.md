# Sprint 4 shell host lifetime foundation

## Goal

Introduce a desktop shell host seam between session startup and the eventual Avalonia window loop so Sprint 4 can keep moving toward a real desktop shell without coupling `DesktopProgramBootstrap` directly to a future UI toolkit implementation.

## Current-state findings

- `Program` and `DesktopProgramBootstrap` currently start a `DesktopShellSession` and then either exit immediately or wait on an injected shutdown task.
- `DesktopShellWindowViewModel` already aggregates the bindable shell state needed by a future desktop window.
- There is no dedicated abstraction representing the long-running shell host/window lifetime.
- `IDesktopShellSession` does not currently expose the shell state needed to compose the existing window view-model outside the concrete `DesktopShellSession` type.

## In scope

- Add an `IDesktopShellHost` abstraction for running the started shell session.
- Expose shell navigation state from `IDesktopShellSession` so window composition can depend on the interface instead of the concrete session type.
- Refactor `DesktopShellWindowViewModel` to depend on `IDesktopShellSession`.
- Route `DesktopProgramBootstrap` through the new shell-host seam.
- Add focused tests for bootstrap/host behavior and disposal.

## Out of scope

- Adding Avalonia packages or views.
- Real window rendering, XAML, or platform lifetimes.
- Changing daemon launch, handshake, or reconnect semantics.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-shell-host-lifetime-foundation.md`

## API, schema and event changes

- No external API changes.
- No schema or migration changes.
- Internal desktop composition gains a dedicated shell-host abstraction.

## Implementation sequence

1. Extend `IDesktopShellSession` with shell state needed for composition.
2. Refactor `DesktopShellWindowViewModel` to depend on the interface.
3. Add `IDesktopShellHost` and a small `DesktopShellWindowHost` implementation.
4. Update `DesktopProgramBootstrap` and `Program` to use the host seam.
5. Extend focused tests for host execution and window-model disposal.

## Testing strategy

- Extend `DesktopProgramBootstrapTests` for non-exit shell-host execution.
- Add focused `DesktopShellWindowHostTests`.
- Keep existing window view-model tests passing with the interface-based constructor.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No new secrets or persistence.
- No license impact.
- Keeps daemon/session ownership inside existing desktop abstractions.

## Risks and stop conditions

- Stop if future UI composition requires a materially different lifetime shape than a single shell host.
- Stop if exposing shell state on `IDesktopShellSession` would leak more than the existing window view-model already consumes.

## Acceptance-criteria mapping

- Strengthens the Avalonia app shell deliverable by creating a clean runtime seam for a future real window host.
- Preserves the existing local-daemon startup flow while moving shell lifetime management out of bootstrap internals.
