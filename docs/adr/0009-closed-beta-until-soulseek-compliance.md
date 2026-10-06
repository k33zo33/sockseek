# ADR-0009: Keep beta closed until Soulseek compliance is resolved

## Status
Accepted

## Context
Sprint 15 requires a written Soulseek compliance decision before any public beta release.

The official Soulseek rules state that alternative clients are tolerated when they implement the full range of network features, including chat, search, wishlist, download, upload and privilege recognition. They also state that automated clients or scripts that fail to implement the full range of features are not allowed to connect. The official Terms of Use place responsibility on users to obey intellectual-property law and avoid infringing uploads/downloads.

Sockseek currently implements search, folder-oriented discovery and download workflows around the existing Sockseek engine, plus local library/player/provider-import features. It does not currently implement or verify desktop/daemon support for Soulseek wishlist, upload/sharing, chat or privilege recognition.

## Decision
Do not publish a public beta with Soulseek network connectivity enabled from the current Sprint 15 state.

Allow closed/internal beta and release-candidate testing only for the existing local-first desktop app, packaging, provider metadata import, local library, player, diagnostics and performance work. Any closed test distribution must state that Soulseek compliance is unresolved and that the build is not a public beta.

Public beta requires a later accepted decision that either:

- implements and verifies the missing Soulseek feature coverage;
- disables Soulseek network connectivity for the public distribution mode; or
- otherwise resolves the compliance risk with an explicit legal/product decision.

## Consequences
- Sprint 15 can continue performance, diagnostics and packaged-smoke work without implying public release readiness.
- Public release notes must list Soulseek compliance as a blocker until superseded.
- Onboarding/release messaging must include legal-use and user-responsibility language before any external beta.
- No provider audio, external-service playback or provider downloading is introduced by this decision.
