# Sprint 15 closed beta package notices

## Goal

Ensure Windows release-candidate staging includes the closed/internal beta notices required by Sprint 15: root `SECURITY.md` and `docs/beta-limitations.md`.

## Current-state findings

- `ReleaseStager.StageWindows` copies `LICENSE`, `THIRD-PARTY-NOTICES`, SBOM, metadata and installer scripts into the staging directory.
- `ReleaseArtifactValidator` does not currently require `SECURITY.md` or `docs/beta-limitations.md`.
- `docs/beta-go-no-go.md` closed beta requirements require testers to receive the beta limitations and security guidance.

## In scope

- Add package validation for `SECURITY.md` and `docs/beta-limitations.md`.
- Copy those files during Windows staging.
- Update packager tests and release checklist documentation.

## Out of scope

- Changing public API contracts, database schema, provider behavior or Soulseek feature scope.
- Marking public beta as GO.
- Re-running the full Windows package smoke unless the local environment can complete it in this turn.

## Files and projects affected

- `Sockseek.Packager`
- `Sockseek.Packager.Tests`
- `docs/release-checklist.md`
- `docs/plans/15-closed-beta-package-notices.md`

## API, schema and event changes

No application API, schema or event contract changes.

## Implementation sequence

1. Add release artifact constants for `SECURITY.md` and `docs/beta-limitations.md`.
2. Require and copy those files in the Windows stager.
3. Require those files in release validation.
4. Update packager tests and checklist documentation.

## Testing strategy

- Run targeted `Sockseek.Packager.Tests`.
- Run `git diff --check`.

## Migration and rollback

No persistence migration. Rollback removes the added package artifact requirements and test updates.

## Security, privacy and license impact

Improves closed-beta safety by ensuring package artifacts include security reporting and beta limitation guidance. No secrets are added to release payloads.

## Risks and stop conditions

Stop if package staging would need to generate per-user or secret-bearing content. Do not weaken AGPL/source artifact checks.

## Acceptance-criteria mapping

- Release notes limitations: staged artifacts include beta limitations.
- Security/crash reporting: staged artifacts include security reporting guidance.
