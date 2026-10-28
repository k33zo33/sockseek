# Soulseek public beta compliance checklist

Sprint 16 keeps public beta distribution blocked until Soulseek compliance risk is resolved by ADR-0009 or a later accepted decision. This checklist is an engineering and product verification aid, not legal advice, and it does not by itself approve a public beta.

Use this checklist only before proposing a public beta artifact with Soulseek network connectivity enabled, or before proposing a superseding decision that disables Soulseek connectivity for public distribution.

## Required evidence header

Record all fields for every checklist run:

| Field | Required value |
| --- | --- |
| Date | Exact local date of the review. |
| Commit | Full Git commit SHA under test. |
| Artifact | Package name, version and target OS. |
| Reviewer | Person performing the review. |
| Soulseek account/session | Redacted account/session identifier; never include passwords, tokens or private credentials. |
| Network mode | `Soulseek enabled` or `Soulseek disabled for public distribution`. |
| Evidence location | Links or paths to test output, screenshots, trace notes and release notes draft. |

## Feature coverage checklist

Every row must be marked `Pass`, `Fail` or `Not applicable by accepted ADR`, with evidence. A `Fail` in any required Soulseek feature keeps public beta blocked.

| Area | Required verification | Evidence |
| --- | --- | --- |
| Search | User can search Soulseek, inspect results and start an explicit download workflow. | Manual smoke notes plus automated gateway/UI coverage. |
| Wishlist | User-facing wishlist behavior is implemented and verified, or a superseding ADR explains why the public artifact does not connect to Soulseek. | Manual smoke notes, tests or ADR reference. |
| Download | User must explicitly initiate downloads; incomplete, failed and cancelled downloads are visible and recoverable. | Manual smoke notes plus automated download/cancel coverage. |
| Upload / sharing | Sharing/upload behavior is implemented, visible and consistent with Soulseek expectations, or public Soulseek connectivity is disabled by accepted ADR. | Manual smoke notes, configuration evidence and tests where available. |
| Chat | Public/private chat behavior required by Soulseek client compatibility is implemented and smoke-tested, or public Soulseek connectivity is disabled by accepted ADR. | Manual smoke notes, screenshots and tests where available. |
| Privileges | Privilege status/recognition is visible or respected by the client, or public Soulseek connectivity is disabled by accepted ADR. | Manual smoke notes, screenshots and tests where available. |
| Folder browse | Browsing remote folders and album-oriented discovery remain functional without path traversal or unsafe filename handling. | Manual smoke notes plus path-safety test evidence. |
| Sharing counts/status | User-visible sharing state does not misrepresent the client on the Soulseek network. | Manual smoke notes and configuration evidence. |
| Legal-use messaging | Onboarding, About/License, release notes and tester instructions state user responsibility without implying every network result is legitimate. | Text review with artifact paths. |
| Provider audio invariant | Spotify, YouTube, Bandcamp and MusicBrainz remain playlist or metadata sources only. | `scripts/run_provider_audio_guard.ps1` output and release-notes review. |

## Manual smoke protocol

Create an artifact-bound evidence directory before starting the manual run. The
helper reads the package's embedded `release-metadata.json`, records its SHA256 and
the detected host, and creates a checklist that contains only a generated redacted
session label unless one is supplied explicitly:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\new_soulseek_live_checklist.ps1 `
  -ArtifactPath .tmp\sprint16-windows-package-smoke-c7b4cc5\Sockseek-win-x64.zip `
  -Reviewer 'release-reviewer' `
  -ExpectedCommit c7b4cc5ba18cc3ce4f9c2b3fa83734b07d67ac0f `
  -RequiredHost Windows11X64
```

Use `-RequiredHost Ubuntu2404X64` for an Ubuntu 24.04 x64 run. Never pass a
Soulseek username or password to the helper. Complete the generated `checklist.md`
with UTC timestamps and redacted evidence, then validate it against the same
unchanged package:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_soulseek_live_checklist.ps1 `
  -ArtifactPath <exact-package-path> `
  -RunDirectory <generated-run-directory>
```

The validator rejects pending or failed rows, missing evidence, mismatched package
hashes/commits, host metadata/classification drift, an invalid required-host
classification and common plaintext, JSON, command-line and Authorization credential
forms. Windows 11
requires an x64 build number of at least 22000, and Ubuntu requires 24.04 x64.
It cannot prove that screenshots or prose are true;
the named reviewer remains responsible for the live observations and redaction.

For the controlled two-peer Windows run, use the manual-only remote workflow through
the host orchestrator after a temporary TCP listener, firewall rule and router mapping
have been explicitly approved. The script verifies the full retained-package hash,
generates both dedicated accounts and control values in memory, passes the remote
bundle to GitHub only over standard input, requires evidence in both directions and
removes the repository control, accounts, daemon and working data before accepting the
checklist:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\run_soulseek_live_checklist.ps1 `
  -ArtifactPath .tmp\sprint16-windows-package-smoke-c7b4cc5\Sockseek-win-x64.zip `
  -Reviewer 'release-reviewer' `
  -CandidateCommit c7b4cc5ba18cc3ce4f9c2b3fa83734b07d67ac0f `
  -ExpectedArtifactSha256 2d41a5f4cedd496ff8ea3e7adac13347634eb922aa7e6f1c07289ab64c210dde `
  -ListenPort 49999 `
  -PortForwardConfirmed
```

The confirmation switch records only operator intent; it does not configure or prove
the router/firewall path. Remove both temporary network rules immediately after the
run, including after a failed run. Never put account names, passwords, probes or chat
control text on the command line or in retained evidence.

1. Start from a clean user data directory or a documented upgrade path.
2. Confirm the daemon binds to loopback and protected `/api/v1` endpoints require the local session token.
3. Connect to Soulseek with a test account and do not record or paste the account password.
4. Exercise the feature coverage checklist above with exact timestamps and screenshots where useful.
5. Export diagnostics through the app flow when available, inspect the export, and redact any private data before attaching it to an issue.
6. Review the exact public beta release notes, About/License surface and package notices for legal-use language, source availability and third-party notices.
7. Validate the Windows ZIP and package-smoke JSON together with `scripts/validate_windows_package_smoke_evidence.ps1 -RequiredHost Windows11X64`.
8. Run the automated gates listed in `docs/beta-go-no-go.md` on the same commit or explain why a newer commit supersedes the run.
9. Run `scripts/validate_soulseek_live_checklist.ps1` against the unchanged package and retain `run-metadata.json`, `checklist.md` and referenced redacted evidence together.

## Stop conditions

- Do not publish a public beta while ADR-0009 remains the active decision for a Soulseek-enabled artifact.
- Do not publish a public beta if wishlist, upload/sharing, chat or privilege recognition is missing or unverified for a Soulseek-enabled artifact.
- Do not publish a public beta if the public artifact disables Soulseek connectivity without an accepted ADR or release decision documenting that mode.
- Do not publish a public beta if release notes, About/License or package notices omit user responsibility, source availability, license or third-party notice requirements.
- Do not attach logs or diagnostics that include provider tokens, OAuth codes, PKCE code verifiers, client secrets, Soulseek passwords, full Authorization headers or private playlist URLs.
