# Threat model

Sprint 14 release-candidate scope covers the local-first desktop app, localhost daemon, local data stores, provider metadata imports, Soulseek downloads and release packaging. External providers are playlist or metadata sources only; they are never audio sources.

## Assets

- Local music library and completed or progressive Soulseek downloads.
- SQLite database, provider snapshots and playback queue state.
- Soulseek credentials, provider access/refresh tokens, OAuth codes and PKCE verifiers.
- Local daemon session token and loopback API access.
- Release artifacts: binaries, `LICENSE`, `THIRD-PARTY-NOTICES`, SBOM, source metadata and installer scripts.

## Trust boundaries

- Desktop to daemon: localhost HTTP and SignalR with a local session token.
- Daemon to filesystem: configured library roots, download output paths, artwork cache, database and secret store.
- Daemon to external services: provider metadata APIs and Soulseek network interactions.
- Release tooling to user machine: staged self-contained Windows payload and user-level install script.

## Threats and mitigations

| Area | Threat | Mitigation / current gate |
| --- | --- | --- |
| Local API exposure | Daemon accidentally listens on LAN and exposes protected endpoints. | Default bind resolves to `http://127.0.0.1:5030`; configured non-loopback URLs are rejected unless an explicit runtime URL is provided. Protected `/api/v1` endpoints require the session token. |
| Local API shutdown | Malicious local caller stops daemon. | `/api/v1/system/shutdown` is protected by the same session-token middleware as other protected versioned endpoints. |
| Session/token disclosure | Logs or diagnostics reveal access tokens, refresh tokens, OAuth codes, code verifiers, client secrets, Soulseek passwords or full Authorization headers. | Diagnostics export and log redaction use `SensitiveLogRedactor`; tests cover common secret key names and Authorization values. |
| File traversal | Remote filenames escape configured download roots. | Download path code sanitizes and canonicalizes remote names before filesystem use; path traversal tests cover escaping names. |
| Symlink traversal | Library scan follows a symlink or junction out of the configured library root. | Local library scan skips reparse points by default; symlink tests cover outside-root traversal. |
| Secret persistence | Disconnect leaves provider token material behind. | Secret references are stored separately through `ISecretStore`; disconnect deletes the secret reference and can remove provider snapshots without touching local music. |
| External-provider scope creep | Spotify, YouTube, Bandcamp or MusicBrainz become audio providers. | Provider contracts expose playlist/metadata capabilities only; `scripts/run_provider_audio_guard.ps1` checks for provider playback/download/audio markers before release. |
| Update data loss | A migration-bearing update overwrites or deletes existing database/config/music. | Database migration runner creates upgrade backups before pending migrations. Installer/uninstaller scripts preserve user data by default. |
| Release artifact tampering or omissions | Binary artifact ships without AGPL notices, exact source metadata, SBOM or native dependencies. | `Sockseek.Packager validate-release` requires legal files, source metadata, SPDX SBOM, installer scripts, Desktop executable and daemon executable. Windows staging smoke checks SQLite and libVLC native files. |
| Dependency vulnerabilities | Known vulnerable NuGet packages ship in release artifacts. | Central package pins resolved the current AngleSharp and SQLitePCLRaw advisories; `dotnet list package --vulnerable --include-transitive` is a release gate. |

## Residual risks

- Windows installer scripts are user-level PowerShell scripts, not signed MSI/MSIX installers. Public distribution still needs code signing/signature and rollback decisions before stable release.
- Docker full build smoke depends on Docker Desktop/Linux engine availability; local validation may be skipped when that engine is unavailable.
- Live provider quotas, consent-screen changes and Soulseek network behavior remain operational risks outside deterministic fixture tests.
- Antivirus or enterprise PowerShell execution policies may block script-based installation; a signed installer remains the preferred public-release target.

## Release gates

Before public binaries are published:

- `dotnet restore`
- `dotnet build -c Release`
- `dotnet test -c Release --no-build`
- `dotnet list package --vulnerable --include-transitive`
- Windows package smoke in `docs/package-smoke.md`
- Forbidden provider-audio symbol scan
- Review `docs/release-checklist.md`
