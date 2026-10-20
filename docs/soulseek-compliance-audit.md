# Soulseek compliance audit

Sprint 15 treats Soulseek compliance as a public-release blocker. This document is a product/engineering audit, not legal advice.

## Sources reviewed

- Soulseek Rules: https://www.slsknet.org/news/node/681
- Soulseek Terms of Use: https://www.slsknet.org/news/node/682

Reviewed on 2026-10-06.

## Relevant requirements

The Soulseek rules say automated clients or scripts that fail to implement the full feature range are not allowed to connect, and that alternative clients are tolerated when they implement the full range of network features, including chat, search, wishlist, download, upload and privilege recognition.

The Terms of Use also place responsibility on users to obey intellectual-property law and not upload or download content in violation of third-party rights.

## Current product coverage

| Area | Current Sockseek status | Public beta risk |
| --- | --- | --- |
| Search | Implemented through existing Sockseek/Soulseek engine flows, with track and album search UI/API. | Low for feature coverage; still subject to acceptable-use messaging. |
| Download | Implemented for explicit user workflows and playlist resolution. | Medium; user responsibility messaging must be clear. |
| Folder browse / album discovery | Implemented through existing album folder projection and download workflows. | Medium; depends on existing engine parity. |
| Wishlist | Implemented across persisted daemon scheduling, authenticated API and Desktop controls with deterministic automated coverage. | Medium; a redacted live-network smoke is still required before public-beta approval. |
| Upload / sharing | Explicit persisted roots feed an immutable audio-only snapshot with canonical containment, reparse-point exclusion and pre-open size/path revalidation. Incoming search/browse/directory requests and uploads use that snapshot, one upload slot is enforced and advertised counts are refreshed from it. Authenticated API/client root CRUD, per-root status, fail-closed invalidation and manual rebuild are implemented. Desktop controls, bounded visible upload state and live-network evidence remain absent. | High; explicitly listed in Soulseek rules and not yet user-visible or live-verified. |
| Chat | Core, daemon/API and Desktop private, room and public workflows are implemented with bounded RAM-only state, replay dedupe, virtualized UI, content-free invalidation, coalesced HTTP snapshot recovery and reconnect restoration. | Medium until the redacted live-network smoke passes. |
| Privileges | Core, daemon/API and Desktop recognition plus explicit acknowledgement are implemented with bounded runtime state and username-free SignalR invalidation. | Medium; a redacted live-network smoke is still required before public-beta approval. |
| Legal-use guidance | Present in Desktop About/License surface, `docs/beta-limitations.md`, generated closed-beta tester instructions and staged Windows package notices. | Low for closed/internal beta; re-review exact public release notes before any public beta. |

## Decision summary

Sockseek is not ready for public beta distribution with Soulseek connectivity enabled.

Closed/internal beta testing may continue for packaging, local library, provider import, player, diagnostics and performance validation, but public release must remain blocked until an accepted ADR either:

- implements and verifies the missing Soulseek feature coverage; or
- defines a distribution mode that does not connect to the Soulseek network; or
- records another explicit compliance/legal decision before public distribution.

The corresponding ADR is `docs/adr/0009-closed-beta-until-soulseek-compliance.md`.

## Required follow-up before public beta

- Complete upload/sharing API, Desktop controls and transfer visibility, then run redacted live verification for wishlist, chat, upload and privilege recognition.
- Re-review user-facing legal-use and user-responsibility messaging in onboarding, About/License and release notes for the exact public beta artifact.
- Use `docs/soulseek-public-beta-compliance-checklist.md` to record feature/compliance tests or manual smoke evidence for every Soulseek feature claim.
- Re-review Soulseek rules and Terms of Use before tagging any public beta.
