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
| Wishlist | Not implemented in the desktop/daemon product. | High; explicitly listed in Soulseek rules. |
| Upload / sharing | Not implemented or verified as a first-class desktop/daemon feature. | High; explicitly listed in Soulseek rules. |
| Chat | Not implemented. | High; explicitly listed in Soulseek rules. |
| Privileges | Not implemented or verified as a visible/respected feature. | High; explicitly listed in Soulseek rules. |
| Legal-use guidance | Present in Desktop About/License surface, `docs/beta-limitations.md`, generated closed-beta tester instructions and staged Windows package notices. | Low for closed/internal beta; re-review exact public release notes before any public beta. |

## Decision summary

Sockseek is not ready for public beta distribution with Soulseek connectivity enabled.

Closed/internal beta testing may continue for packaging, local library, provider import, player, diagnostics and performance validation, but public release must remain blocked until an accepted ADR either:

- implements and verifies the missing Soulseek feature coverage; or
- defines a distribution mode that does not connect to the Soulseek network; or
- records another explicit compliance/legal decision before public distribution.

The corresponding ADR is `docs/adr/0009-closed-beta-until-soulseek-compliance.md`.

## Required follow-up before public beta

- Decide whether to implement wishlist, upload/sharing, chat and privilege recognition in scope.
- Re-review user-facing legal-use and user-responsibility messaging in onboarding, About/License and release notes for the exact public beta artifact.
- Add feature/compliance tests or manual smoke checklist for every Soulseek feature claim.
- Re-review Soulseek rules and Terms of Use before tagging any public beta.
