# Sprint 14 Packaging, Legal and Security Hardening

## Goal

Produce a release-candidate path that can package the local-first desktop app, daemon, SQLite/native media dependencies and required AGPL/legal/security artifacts without exposing secrets or weakening localhost security.

## Current-state findings

- `Sockseek.Packager` exists and can create deterministic `tar.gz` archives from a publish directory, but it does not yet assemble Desktop/Server publish outputs or verify legal artifacts.
- `THIRD-PARTY-NOTICES`, `LICENSE`, `docs/LEGAL.md`, `docs/SECURITY.md`, `docs/TESTING.md` and `docs/release-checklist.md` already define the release gate.
- CI currently restores, builds and tests on Ubuntu, but it does not run OpenAPI drift, dependency/license scans, SBOM generation or package smoke checks.
- `Dockerfile` targets `net10.0` but still publishes `Sockseek.Cli`; the Sprint 14 goal calls for a daemon container refresh while keeping Docker secondary to desktop packaging.
- Existing daemon/desktop tests already cover loopback/session-token basics, restart handling and many UI states; Sprint 14 should add focused packaging/security tests instead of broad retesting.
- Existing dependency advisories remain for `AngleSharp` and `SQLitePCLRaw.lib.e_sqlite3`; Sprint 14 must either upgrade safely or document/block release if an upgrade is not viable.

## In scope

- Add version/commit/source metadata to API and Desktop About/License surfaces.
- Extend packaging tooling to stage Desktop, daemon, `LICENSE`, `THIRD-PARTY-NOTICES`, source metadata and native runtime dependencies for Windows x64 first.
- Add tests/checks that release artifacts include required legal/source files.
- Add diagnostics/log export redaction checks for secrets, tokens, OAuth codes and Authorization headers.
- Add user-data directory and upgrade backup smoke coverage where packaging/runtime startup needs it.
- Refresh Docker daemon packaging/docs after the Windows packaging path is stable.
- Add focused security tests for path traversal, localhost bind/auth defaults, OAuth callback handling and secret deletion where gaps are found.

## Out of scope

- Auto-update, code signing infrastructure, remote daemon mode, LAN auth, provider write-back or provider audio.
- Linux package completion before Windows packaging is stable.
- Replacing the Soulseek engine or player engine.
- Legal advice beyond repository release-gate automation and notices.

## Files and projects affected

- `Sockseek.Packager` for staging, archive creation and release checklist validation.
- `Sockseek.Desktop` and `Sockseek.Desktop.Tests` for About/License UI and local diagnostics/export surfaces.
- `Sockseek.Server`, `Sockseek.Api` and tests for build/source metadata and security info endpoints if needed.
- `Sockseek.Server.Tests`, `Sockseek.Infrastructure.Tests` and `Sockseek.Desktop.Tests` for security/package smoke tests.
- `.github/workflows/*`, `Dockerfile`, `docs/docker.md`, `docs/release-checklist.md`, `THIRD-PARTY-NOTICES` and possibly generated SBOM files.

## API, Schema and Event Changes

- Prefer no database schema change.
- If an API info endpoint is needed, expose only non-secret build metadata: version, commit, source URL, license and runtime mode.
- No provider contracts, audio-source policy or daemon remote-bind behavior may change without an ADR.

## Implementation Sequence

1. Add release metadata plumbing and tests: version, commit and source URL available to Server/Desktop without secrets.
2. Add About/License surface and Desktop tests for AGPL, source URL, version and commit.
3. Extend `Sockseek.Packager` with a Windows x64 staging/check command that verifies `LICENSE`, `THIRD-PARTY-NOTICES`, Desktop/daemon binaries and metadata.
4. Add package checklist tests and run a local publish/package smoke where feasible.
5. Add redaction helpers/tests for diagnostics/log export and ensure Authorization/token/OAuth/secret values are scrubbed.
6. Audit localhost bind/auth, OAuth callback and path traversal/symlink protections; add failing tests before fixes where gaps exist.
7. Address dependency advisories with safe upgrades or document release-blocking exceptions.
8. Refresh Dockerfile/docs for the daemon container path after primary packaging gates are in place.
9. Run full validation, package smoke, provider-audio scan, update Sprint 14 status only when acceptance criteria pass.

## Testing Strategy

- Existing baseline: `dotnet restore`, `dotnet build -c Release`, `dotnet test -c Release --no-build`.
- Add targeted `Sockseek.Packager` tests for artifact layout/legal files.
- Add Desktop tests for About/License text and source/version/commit display.
- Add security integration tests for auth/bind defaults, redaction, traversal/symlink handling and secret deletion.
- Add package smoke commands for Windows x64 publish/stage/archive when local environment supports it.
- Keep provider-audio forbidden-symbol scans in every final Sprint 14 validation pass.

## Migration and Rollback

- No persistence migration is expected.
- Packaging changes should be reversible by removing generated/staged artifacts; no user music, database or config deletion is allowed.
- Upgrade backup behavior must be additive and tested before any migration-bearing release candidate.

## Security, Privacy and License Impact

- Release artifacts must include AGPL license text, third-party notices and exact source metadata.
- Diagnostics/log export must redact access tokens, refresh tokens, OAuth codes, code verifiers, client secrets, Soulseek passwords and Authorization headers.
- Daemon remains loopback-bound by default and protected by local session token.
- Packaged outputs must not include local user database, config, provider secrets or Soulseek credentials.

## Risks and Stop Conditions

- Stop and request an ADR for remote daemon auth, provider audio, provider downloads or broad process-topology changes.
- Stop release progression if legal artifacts, source metadata, SBOM/notices, redaction or localhost auth gates cannot be automated.
- Treat dependency advisories in redistributed runtime packages as release blockers unless accepted and documented.
- Do not publish public binaries from this sprint until package smoke and legal/security checklist tests pass.

## Acceptance-Criteria Mapping

- Clean Windows install: Windows x64 staging/publish smoke plus packaged native dependency checks.
- Update preserves data: user-data directory and backup smoke tests.
- About shows AGPL/source/version/commit: About metadata implementation and Desktop tests.
- Secrets absent from log export: redaction helpers and security tests.
- Daemon not open to LAN by default: bind/auth tests.
- SBOM and notices in artifacts: packager validation and release checklist tests.
