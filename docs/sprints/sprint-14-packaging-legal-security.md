## Sprint 14 - Packaging, pravne obavijesti i security hardening

## Status

Completed

## Required context

Always read `/AGENTS.md` and `/docs/project-state.yaml`, then:

- [LEGAL.md](../LEGAL.md)
- [SECURITY.md](../SECURITY.md)
- [TESTING.md](../TESTING.md)

## Scope rule

Do not implement future sprint scope. Stop and request an ADR if a locked decision must change.

> **Cilj sprinta**  
> Izraditi release candidate koji se sigurno instalira i ispunjava AGPL obveze.

Ovisnosti: Svi funkcionalni MVP sprintovi.

### Isporučivi rezultati

- Windows self-contained installer; Linux package nakon Windows stabilnosti.

- Ažurirani .NET 10 daemon Dockerfile.

- About/License/Source UI.

- Threat model, SBOM i dependency scan.

### Implementacijski zadaci

1. Pakirati Desktop, daemon, SQLite native i media native dependencies.

1. Implementirati user-data direktorije i upgrade backup.

1. Dodati log export s redactionom.

1. Dodati source commit/version u About i API info.

1. Dodati LICENSE i THIRD-PARTY-NOTICES u paket.

1. Auditirati file traversal, localhost auth, OAuth callback i secret deletion.

1. Dodati crash recovery i clean shutdown child procesa.

### Acceptance kriteriji

- Čista Windows instalacija pokreće aplikaciju bez ručne .NET instalacije.

- Update ne briše bazu, config ili glazbu.

- About prikazuje AGPL, source link, version i commit.

- Secrets nisu u log exportu.

- Daemon nije otvoren prema LAN-u po defaultu.

- SBOM i third-party notices dio su release artefakta.

### Obavezni testovi

- Fresh install/upgrade/uninstall smoke tests.

- Security integration tests.

- License/source artifact checklist test.

- Path traversal and symlink tests.

> **Izlazni artefakt sprinta**  
> Potpisan ili interno verificiran release candidate s kompletnim source/legal paketom.

## Completion report

Report changed files, validation commands and results, migrations, security/license impact, known risks and every unmet acceptance criterion.

Completed on 2026-10-06.

Changed files and areas:

- `Sockseek.Server` and `Sockseek.Api`: release metadata endpoint/source metadata wiring, daemon loopback bind hardening and upgrade backup coverage.
- `Sockseek.Desktop`: About/License/source metadata surface and clean shutdown of the supervised daemon process.
- `Sockseek.Packager`: Windows staging validator, SBOM generator, installer/uninstaller scripts, release zip creation and SHA256 manifest generation.
- `Sockseek.Application`, `Sockseek.Infrastructure`, `Sockseek.Server` and tests: diagnostics redaction, symlink/path-safety hardening, OAuth callback checks, protected provider/account endpoints and secret deletion coverage.
- `Dockerfile` and `docs/docker.md`: daemon executable included in the secondary Docker path; full Docker build smoke remains environment-blocked locally.
- Legal/security docs: `docs/package-smoke.md`, `docs/threat-model.md`, `docs/release-checklist.md`, `docs/SECURITY.md`, `docs/INDEX.md` and this sprint plan/report.

Validation:

- `dotnet build -c Release` passed with existing Desktop test fake-event CS0067 warnings.
- `dotnet test -c Release --no-build` passed across all test projects: Architecture 5, Application 55, Domain 26, Player 34, Packager 13, Core 578, Infrastructure 72, CLI 254, Server 154, Desktop 227.
- `dotnet list package --vulnerable --include-transitive` passed; no vulnerable packages were reported for any project.
- Provider-audio forbidden symbol scan across production projects returned no matches.
- Windows package smoke on the current release path passed: Desktop and daemon `win-x64` self-contained publishes, SBOM generation with 126 packages, Windows staging validation, release zip creation and SHA256 manifest generation.
- Staged artifact spot checks passed for `Sockseek.Desktop.exe`, `daemon/Sockseek.Server.exe`, `LICENSE`, `THIRD-PARTY-NOTICES`, `release-metadata.json`, `sbom.spdx.json`, `install.ps1`, `uninstall.ps1`, `daemon/e_sqlite3.dll` and `daemon/libvlc/win-x64/libvlc.dll`.

Migrations:

- No EF migration was added in Sprint 14.
- Existing migration runner backup behavior is covered by upgrade backup tests; installer/uninstaller scripts preserve user data by default.

Security, privacy and license impact:

- Release artifacts include AGPL license text, third-party notices, exact source metadata, SBOM, installer scripts and checksum manifest.
- About/API metadata expose version, commit, source URL and AGPL license without secrets.
- Diagnostics export redacts access tokens, refresh tokens, OAuth codes, code verifiers, client secrets, session tokens, Authorization headers and Soulseek passwords.
- Daemon bind defaults remain loopback-only and `/api/v1` provider/account endpoints remain protected by the local session token.
- Disconnecting an external account deletes the stored secret while preserving provider playlist rows and local music.
- No provider playback, provider audio URL, provider stream URL or provider download capability was introduced.

Known risks:

- Windows installer scripts are user-level PowerShell scripts, not signed MSI/MSIX installers. Public distribution still needs signing/rollback decisions before a stable public release.
- Full Docker build smoke could not be completed locally because Docker Desktop's engine pipe was unavailable, although the Dockerfile daemon publish path is updated and documented.
- Linux desktop packaging is not complete. The current player native dependency is the Windows LibVLC package; cross-platform native packaging remains follow-up work before declaring cross-platform stable.
- `THIRD-PARTY-NOTICES` is bundled and validated, but public release notes/source tag creation remain per-release operations.

Unmet Sprint 14 acceptance criteria:

- None known for the Windows release-candidate path after validation.
