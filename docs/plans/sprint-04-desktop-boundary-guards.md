# Sprint 4 desktop boundary guards

## Goal

Add executable guardrails for the Sprint 4 desktop boundary acceptance criteria so future shell work does not accidentally pull in `Sockseek.Core` or EF `DbContext` usage.

## Current-state findings

- `Sockseek.Desktop` currently references `Sockseek.Api` and has no direct `Sockseek.Core` project reference.
- The sprint acceptance criteria explicitly require that desktop not reference `Sockseek.Core` or `DbContext`.
- There is no automated regression test covering those architectural boundaries today.

## In scope

- Add focused desktop tests that verify the desktop assembly does not reference `Sockseek.Core`.
- Verify the desktop project file does not directly reference `Sockseek.Core`.
- Scan desktop source files for `DbContext` and `Microsoft.EntityFrameworkCore` usage.

## Out of scope

- Full repository architecture linting.
- Refactoring desktop/API boundaries.
- Changes to server, domain, or persistence layers.

## Files and projects affected

- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-desktop-boundary-guards.md`

## API, schema and event changes

- No API changes.
- No schema or migration changes.
- Test-only architecture guardrails.

## Implementation sequence

1. Add a desktop architecture guard test file.
2. Verify compiled assembly references and project references.
3. Verify desktop source has no `DbContext`/EF usage.
4. Run desktop tests.

## Testing strategy

- Run `Sockseek.Desktop.Tests` in Docker.
- Keep the new tests file-system based and deterministic.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- No runtime behavior changes.
- Reinforces the local desktop architecture boundary.
- No license impact.

## Risks and stop conditions

- Stop if a currently intentional desktop dependency on `Sockseek.Core` is discovered.
- Stop if the repository layout makes stable source-root discovery impossible in tests.

## Acceptance-criteria mapping

- Directly guards: `Desktop nema referencu na Sockseek.Core ni DbContext.`
- Helps preserve the desktop shell scope while Sprint 4 continues.
