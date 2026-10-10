# Sprint 15 closed beta instructions package

## Goal

Ensure Windows release-candidate smoke artifacts include generated closed/internal beta tester instructions stamped with the exact package commit.

## Current-state findings

- `scripts/write_closed_beta_tester_instructions.ps1` can generate per-build tester instructions with commit, source, legal-use, limitation and diagnostics guidance.
- `scripts/run_windows_package_smoke.ps1` currently stages required static notices but does not generate or include the per-build tester instructions in the archive.
- `docs/beta-go-no-go.md` requires exact commit tester instructions for closed/internal beta.

## In scope

- Generate `closed-beta-tester-instructions.md` into the Windows staging directory during package smoke.
- Verify the instructions file is present in staging, ZIP archive and SHA256 manifest.
- Add a standalone generator smoke so closed/internal-only status, ADR-0009 notice, provider no-audio language and diagnostics redaction guidance are validated without a full package run.
- Update beta go/no-go evidence to describe the strengthened smoke.

## Out of scope

- Changing application runtime packaging, public API contracts or provider behavior.
- Marking public beta as GO.
- Generating public release notes or source tags.

## Files and projects affected

- `scripts/run_windows_package_smoke.ps1`
- `docs/beta-go-no-go.md`
- `docs/plans/15-closed-beta-instructions-package.md`

## API, schema and event changes

No application API, schema or event changes.

## Implementation sequence

1. Add a staged tester instructions path in `run_windows_package_smoke.ps1`.
2. Invoke `scripts/write_closed_beta_tester_instructions.ps1` after staging and before archive creation.
3. Validate the staged file, archive entry and manifest entry.
4. Add and run `scripts/test_closed_beta_tester_instructions.ps1`.
5. Run Windows package smoke where feasible.

## Testing strategy

- Run `scripts/run_windows_package_smoke.ps1 -Force` and inspect archive/manifest evidence.
- Run `scripts/test_closed_beta_tester_instructions.ps1` for fast generator coverage without publishing artifacts.
- Run `git diff --check`.

## Migration and rollback

No persistence migration. Rollback removes the smoke-script generation and validation step.

## Security, privacy and license impact

Generated tester instructions are assembled from committed public documentation and include no provider tokens, OAuth codes, Soulseek passwords or Authorization headers.

## Risks and stop conditions

Stop if generating instructions would require user-specific or secret-bearing data. Do not change the public beta NO-GO decision.

## Acceptance-criteria mapping

- Release notes limitations: closed beta package includes exact-commit tester instructions with limitations.
- Security/crash reporting: tester instructions include diagnostics redaction guidance.
