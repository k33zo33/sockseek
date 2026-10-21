# Sprint 16 release-candidate evidence

## Candidate

| Field | Value |
| --- | --- |
| Date | 2026-10-21 (Europe/Zagreb) |
| Commit | `c7b4cc5ba18cc3ce4f9c2b3fa83734b07d67ac0f` |
| Version | `3.0.5` |
| Network mode | Soulseek enabled |
| Decision | `NO-GO` for public beta |

This is engineering evidence, not legal advice. ADR-0009 remains accepted and
is not superseded by this document.

## Automated gates

| Gate | Result |
| --- | --- |
| `dotnet restore Sockseek.sln` | Pass; 28-project solution graph restored. |
| `dotnet build Sockseek.sln -c Release --no-restore` | Pass; 0 warnings, 0 errors, generated OpenAPI unchanged. |
| `dotnet test Sockseek.sln -c Release --no-build` | Pass; 1,584/1,584 tests: Architecture 5, Domain 32, Application 57, Packager 16, Core 598, Player 34, Infrastructure 109, CLI 258, Desktop 284 and Server 191. |
| `scripts/run_provider_audio_guard.ps1` | Pass; 467 production files and 314 provider-boundary files scanned. |
| `dotnet list Sockseek.sln package --vulnerable --include-transitive` | Pass; no project reported a known vulnerable package from the configured sources. |
| Release/compliance validators | Pass; beta go/no-go, Soulseek compliance, diagnostics-feedback, closed-beta instructions and Docker-diagnostics validators all preserve the current public-beta block and redaction requirements. |
| GitHub Actions | Pass; manual workflow run `39661044471` built commit `c7b4cc5`, installed the Ubuntu LibVLC runtime, passed the full test project and exercised the artifact-bound Soulseek evidence helper gate. |
| Docker/headless smoke | Pass on exact commit `c7b4cc5`: Docker Desktop Linux-engine access, compose validation, image build, packaged CLI help, daemon `/health` on `127.0.0.1:5030` and bounded container cleanup all returned exit code 0. Diagnostics are retained locally at `.tmp/sprint16-docker-smoke-c7b4cc5.md`. |

## Package evidence

| Target | Evidence | Result |
| --- | --- | --- |
| Ubuntu 24.04 x64 | `Sockseek-linux-x64.tar.gz`; SHA256 `ee7072fba179799c2911b49bb75860b6d82406426a53945e6c1a8198a25670fd`. Exact archive passed daemon `/health`, anonymous protected-API rejection, authenticated Soulseek account configure, daemon restart/restore and disconnect, response and encrypted-payload plaintext checks, `0700` secret-directory and `0600` key/payload checks, secret cleanup, packaged Desktop child-daemon health under Xvfb, install/uninstall and data preservation. | Pass |
| Windows 10 Pro x64 preflight | `Sockseek-win-x64.zip`; SHA256 `2d41a5f4cedd496ff8ea3e7adac13347634eb922aa7e6f1c07289ab64c210dde`. Evidence JSON records Windows `10.0.19045`, build `19045`, 64-bit. Exact ZIP passed direct daemon and Desktop child-daemon health, anonymous protected-API rejection, loopback-only listener checks and the DPAPI-backed Soulseek account configure, daemon restart/restore and disconnect lifecycle without response/plaintext leakage or a retained secret payload. | Pass as preflight only |
| Windows 11 x64 | No run is recorded on a Windows 11 host for this candidate. Windows 10 evidence is not substituted. | Missing |

The exact Linux package excludes `VideoLAN.LibVLC.Windows`; the Ubuntu runtime
supplies `vlc` and `libvlc-dev`. Linux staging rejects a future
`libvlc/win-*` payload. Both package smokes use generated ephemeral credentials,
never print them and disconnect before cleanup; they do not constitute a live
Soulseek-network checklist run.

## Live Soulseek checklist

No dedicated-account live-network checklist run is recorded for this candidate.
Automated adapter/API/Desktop coverage is green, but it is not a substitute for
protocol evidence. The following required rows therefore remain unverified:

| Area | Current evidence state |
| --- | --- |
| Search and explicit download | Automated coverage present; exact-candidate live notes missing. |
| Wishlist | Automated persistence, scheduling, recovery and UI coverage present; live notes missing. |
| Upload / sharing and advertised counts | Automated path, snapshot, upload and UI coverage present; remote-peer live evidence missing. |
| Chat | Automated private, room and public chat coverage present; live notes missing. |
| Privileges | Automated recognition and acknowledgement coverage present; live notes missing. |
| Folder browse | Automated browse/path-safety coverage present; live notes missing. |

No account name, password, token, private-message content or private playlist URL
is recorded in this evidence.

## Public-beta decision

Public beta remains `NO-GO` because:

1. the redacted dedicated-account live Soulseek checklist is incomplete;
2. the exact Windows 11 x64 package smoke is missing; and
3. ADR-0009 remains the accepted decision, so no superseding ADR or public-beta
   release notes may be accepted yet.

Closed/internal testing remains permitted under the existing limitations and
tester instructions.
