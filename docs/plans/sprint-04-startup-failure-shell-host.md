# Sprint 4 startup failure shell hosting

## Goal

Keep the desktop shell running when the initial daemon launch does not succeed so Sprint 4 can surface backend disconnected/retry UX instead of exiting immediately.

## Current-state findings

- `DesktopProgramBootstrap` currently returns exit code `2` whenever `session.StartAsync()` is false, even in the normal long-running desktop mode.
- `DesktopShellWindowViewModel` already exposes disconnected diagnostics and start-daemon retry affordances for future UI binding.
- `DesktopShellSession.StartAsync()` returns `false` without updating the shell state when no local launch path is available, which leaves the shell stuck in `Starting` if hosted after a failed startup.

## In scope

- Adjust desktop bootstrap flow so non-`--exit-after-startup` runs still enter the shell host after an initial startup failure.
- Ensure failed/unavailable startup leaves the shell in a disconnected state suitable for retry UX.
- Add focused bootstrap/session tests for the new behavior.

## Out of scope

- Full Avalonia window implementation.
- New daemon launch semantics or retry throttling.
- Packaging/runtime changes.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-startup-failure-shell-host.md`

## API, schema and event changes

- No API changes.
- No schema or migration changes.
- Desktop startup semantics change so the shell can render backend failure state instead of exiting.

## Implementation sequence

1. Update shell-session startup failure handling to expose disconnected state when launch is unavailable or fails.
2. Update program bootstrap to keep the shell host alive after failed startup in normal interactive mode while preserving `--exit-after-startup` behavior.
3. Extend targeted tests.
4. Run desktop tests.

## Testing strategy

- Extend `DesktopProgramBootstrapTests` for interactive startup-failure hosting behavior.
- Extend `DesktopShellSessionTests` for unavailable-launch disconnected state.
- Run `dotnet test -c Release --no-build` if a fresh build is available, otherwise targeted desktop tests with build.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- Reuses existing local-only daemon/session flow.
- No new secret handling; retry UX continues to use the existing safe diagnostics surface.
- No license impact.

## Risks and stop conditions

- Stop if startup failures must remain fatal for packaging/runtime reasons not yet documented.
- Stop if disconnected-state semantics conflict with planned external-daemon attachment behavior.

## Acceptance-criteria mapping

- Strengthens the backend starting/restarting/disconnected UX deliverable.
- Makes the development shell more user-friendly when the daemon cannot be launched on the first attempt.
