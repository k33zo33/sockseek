# Sprint 16 release-candidate evidence

## Candidate

| Field | Value |
| --- | --- |
| Date | 2026-10-21 (Europe/Zagreb) |
| Commit | `ab3794edf9994c4f82b311ad0aa283e1535d754a` |
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
| `dotnet test Sockseek.sln -c Release --no-build` | Pass; 1,565/1,565 tests: Architecture 5, Domain 32, Application 55, Packager 15, Core 598, Player 34, Infrastructure 100, CLI 258, Desktop 281 and Server 187. |
| `scripts/run_provider_audio_guard.ps1` | Pass; 460 production files and 307 provider-boundary files scanned. |
| `dotnet list Sockseek.sln package --vulnerable --include-transitive` | Pass; no project reported a known vulnerable package from the configured sources. |

## Package evidence

| Target | Evidence | Result |
| --- | --- | --- |
| Ubuntu 24.04 x64 | `Sockseek-linux-x64.tar.gz`; SHA256 `22824efd0a514c60ae4e4d29cbd5bd376e2e8d193f6d43fd2366fa4a6fb4cf68`. Exact archive passed daemon `/health`, packaged Desktop child-daemon health under Xvfb, install/uninstall and data-preservation checks. | Pass |
| Windows 10 Pro x64 preflight | `Sockseek-win-x64.zip`; SHA256 `6287af6678423153282350d281a073aa25eb09ccc5c9c1b687a03d66c76eb9ad`. Evidence JSON records Windows `10.0.19045`, build `19045`, 64-bit. Exact ZIP passed direct daemon and Desktop child-daemon health, anonymous protected-API rejection and loopback-only listener checks. | Pass as preflight only |
| Windows 11 x64 | No run is recorded on a Windows 11 host for this candidate. Windows 10 evidence is not substituted. | Missing |

The exact Linux package excludes `VideoLAN.LibVLC.Windows`; the Ubuntu runtime
supplies `vlc` and `libvlc-dev`. Follow-up hardening in this evidence update
makes Linux staging reject a future `libvlc/win-*` payload.

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
