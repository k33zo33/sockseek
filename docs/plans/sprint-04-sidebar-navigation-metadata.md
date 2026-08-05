# Sprint 4 sidebar navigation metadata

## Goal

Add localization-ready label and shortcut-hint metadata to sidebar navigation items so the desktop shell has real navigation semantics for future labels/tooltips and keyboard discovery.

## Current-state findings

- `ShellNavigationItem` currently exposes only section, display name, icon token, and design tokens.
- Keyboard shortcuts exist in `ShellNavigationViewModel`, but sidebar items do not surface them.
- The UI/UX spec requires keyboard accessibility and localization-ready shell resources.

## In scope

- Add sidebar navigation title resource keys.
- Add sidebar navigation shortcut hint text and resource keys.
- Route sidebar item construction through shared string resources.
- Extend shell tests to verify item metadata.

## Out of scope

- Actual Avalonia tooltips or sidebar views.
- New navigation sections.
- Runtime localization switching.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-sidebar-navigation-metadata.md`

## API, schema and event changes

- No API changes.
- No schema or migration changes.
- Desktop-only navigation metadata additions.

## Implementation sequence

1. Add sidebar navigation hint resources.
2. Expose resource/hint metadata on `ShellNavigationItem`.
3. Update tests for the new metadata.
4. Run desktop tests.

## Testing strategy

- Extend shell navigation tests to verify sidebar resource keys and hints.
- Run `Sockseek.Desktop.Tests` in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No secrets, auth, or persistence changes.
- No license impact.
- Pure desktop metadata scaffolding.

## Risks and stop conditions

- Stop if sidebar metadata needs actual view framework decisions first.
- Stop if resource naming needs a broader localization ADR.

## Acceptance-criteria mapping

- Strengthens the Sprint 4 `sidebar, routing` deliverable.
- Supports the keyboard accessibility requirement for discoverable navigation shortcuts.
