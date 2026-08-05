# Sprint 4 localization resource catalog

## Goal

Back the desktop shell's existing resource keys with a typed default string catalog so Sprint 4 has real localization-ready resources instead of only ad hoc key strings.

## Current-state findings

- Shell pages and command palette already expose resource keys.
- The current desktop models still hardcode most visible strings inline.
- There is no shared desktop string catalog that resolves the existing keys.

## In scope

- Add a typed desktop string resource catalog with default English strings.
- Route shell page, sidebar, command palette, player placeholder, and backend banner text through the catalog.
- Expose missing resource keys on models that currently only carry plain strings.
- Add focused tests for resource lookup and model resource metadata.

## Out of scope

- Avalonia XAML resource dictionaries.
- Non-English translations.
- Full runtime localization switching.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-localization-resource-catalog.md`

## API, schema and event changes

- No API changes.
- No schema or migration changes.
- Desktop-only resource catalog additions.

## Implementation sequence

1. Add a shared desktop string resource catalog.
2. Update shell models to use catalog-backed defaults and explicit resource keys.
3. Add tests for lookup coverage and shell resource metadata.
4. Run desktop tests.

## Testing strategy

- Add deterministic unit tests for resource resolution.
- Extend shell view-model tests to cover new resource-key metadata.
- Run `Sockseek.Desktop.Tests` in Docker.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No secrets or sensitive data involved.
- No license impact.
- Pure desktop UX scaffolding.

## Risks and stop conditions

- Stop if a broader localization framework decision is needed before a simple catalog can land.
- Stop if wiring the catalog would force Avalonia-specific implementation outside Sprint 4 scope.

## Acceptance-criteria mapping

- Strengthens: `Uvesti theme, localization-ready resources i command palette skeleton.`
- Preserves Sprint 4 placeholder pages while making their resource keys executable.
