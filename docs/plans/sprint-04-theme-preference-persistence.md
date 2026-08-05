# Sprint 4 theme preference persistence

## Goal

Make the desktop shell actually remember the user theme across app runs so Sprint 4's light/dark persistence acceptance criterion is true in the executable, not only in memory during tests.

## Current-state findings

- `ShellNavigationViewModel` persists theme changes only to the provided `IDesktopThemePreferenceStore`.
- The default store implementation is currently `InMemoryDesktopThemePreferenceStore`.
- `Program` constructs `DesktopShellSession` without a persistent theme store, so real app runs do not remember theme choice.

## In scope

- Add a file-backed desktop theme preference store.
- Add a small path helper for the desktop settings location.
- Wire the real program/session startup path to use the persistent store.
- Add tests for persistence, invalid-file fallback, and session-level usage.

## Out of scope

- Full Avalonia theme resource application.
- Additional persisted desktop settings beyond theme preference.
- Database or server-side preferences.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-theme-preference-persistence.md`

## API, schema and event changes

- No API changes.
- No schema or migration changes.
- Desktop-local file persistence only.

## Implementation sequence

1. Add file-backed theme store and settings path helper.
2. Wire `Program` to create a persistent store for `DesktopShellSession`.
3. Add unit tests for the store and session persistence behavior.
4. Run desktop tests.

## Testing strategy

- Add deterministic temp-directory tests for the file-backed store.
- Reuse desktop shell session tests to validate persistence through the injected store.
- Run `Sockseek.Desktop.Tests` in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.
- The theme settings file is optional and recreated on demand.

## Security, privacy and license impact

- Stores only a local theme enum value in a user-local settings file.
- No secrets, tokens, or credentials are written.
- No license impact.

## Risks and stop conditions

- Stop if the settings path needs a broader product decision about config layout.
- Stop if persistence requires Avalonia-specific lifecycle hooks outside current Sprint 4 scope.

## Acceptance-criteria mapping

- Directly addresses: `Light/dark tema se pamti.`
- Strengthens the Sprint 4 development shell as a real runnable desktop foundation.
