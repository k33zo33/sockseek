## Sprint 16 - Soulseek public-beta compliance

## Status

In progress

## Required context

Always read `/AGENTS.md` and `/docs/project-state.yaml`, then:

- [ARCHITECTURE.md](../ARCHITECTURE.md)
- [current-api.md](../current-api.md)
- [SECURITY.md](../SECURITY.md)
- [TESTING.md](../TESTING.md)
- [soulseek-compliance-audit.md](../soulseek-compliance-audit.md)
- [soulseek-public-beta-compliance-checklist.md](../soulseek-public-beta-compliance-checklist.md)
- [ADR-0009](../adr/0009-closed-beta-until-soulseek-compliance.md)

## Scope rule

Continue through the existing `Sockseek.Core` / Soulseek.NET boundary. Do not replace the Soulseek engine, add provider audio, expose mutable Core jobs to new application/UI layers or relax loopback/session-token security. Public beta remains blocked until every acceptance criterion passes and a superseding ADR is accepted.

> **Sprint goal**  
> Implement and verify the missing Soulseek network feature coverage required for a public-beta decision, then produce target-OS package evidence and an explicit superseding go/no-go ADR.

Dependency: Sprint 15.

### Deliverables

- [Soulseek.NET capability inventory](../soulseek-net-capability-matrix.md) against the installed API and the existing Sockseek adapter.
- Local-first wishlist scheduling with persisted entries and explicit user controls.
- Chat support required by the compliance checklist, including bounded/redacted local state and reconnect behavior.
- Real upload/sharing configuration, browse/search exposure and transfer visibility; advertised counts must reflect actual configured shares.
- Privilege status/notification recognition and acknowledgement UX.
- Updated daemon contracts, OpenAPI, Desktop surfaces and end-to-end failure/reconnect tests.
- Completed Soulseek public-beta checklist with redacted live-network evidence or a documented failing row.
- Windows 11 and Ubuntu LTS package smoke evidence for the exact release candidate.
- Superseding ADR and exact public-beta release notes only if all gates pass.

### Implementation tasks

1. Inventory Soulseek.NET methods/events/options for wishlist-equivalent searches, room/private chat, uploads/shares and privileges; map each checklist row to an owned Core/Server/Desktop boundary.
2. Define immutable API contracts and persistence for wishlist, chat state, share configuration, upload state and privilege status without exposing credentials or mutable Core objects.
3. Implement wishlist behavior end to end with bounded scheduling, cancellation, reconnect recovery and local persistence.
4. Implement chat and privilege recognition with bounded message retention, explicit user actions and secret/private-data-safe diagnostics.
5. Implement actual sharing/upload behavior with canonicalized roots, traversal protection, symlink/junction policy, transfer controls and truthful advertised counts.
6. Add Desktop workflows and recovery UX for each feature; retain keyboard/accessibility and large-list virtualization requirements.
7. Run automated security, compatibility, provider-audio and failure-matrix gates plus redacted live Soulseek checklist verification.
8. Build and smoke exact packages on Windows 11 x64 and Ubuntu LTS x64; update release notes and propose a superseding ADR only after all evidence passes.

### Acceptance criteria

- Search, wishlist, explicit download, upload/sharing, required chat, privileges, folder browse and sharing status each have automated coverage plus recorded manual/live evidence where the protocol cannot be fully simulated.
- Advertised share counts derive from configured, readable and approved share roots; remote paths cannot escape those roots and no local file is exposed implicitly.
- Wishlist/chat/upload operations recover from disconnects without duplicate unbounded work, message growth or hidden transfers.
- Secrets, private messages and full private playlist URLs are absent from logs, diagnostics and persisted non-secret tables unless an explicit local retention setting requires the data.
- Legacy CLI and daemon API behavior remain compatible; additive `/api/v1` contracts and OpenAPI are tested.
- The provider-audio guard remains green and external providers remain metadata/playlist sources only.
- The public-beta checklist has no `Fail` rows for a Soulseek-enabled artifact, target-OS package smoke passes on the same release commit, and an accepted ADR supersedes ADR-0009 before public distribution.

### Required tests

- Soulseek adapter contract tests for every new method/event mapping.
- SQLite migration and upgrade tests for every persisted model.
- Server auth, reconnect, bounded-retention and path-adversarial integration tests.
- Desktop ViewModel/headless tests for command availability, recovery and redaction states.
- End-to-end mock workflows for wishlist, chat, upload/sharing and privileges.
- Redacted live-network checklist smoke using a dedicated test account.
- Full solution, provider-audio guard, dependency scan and exact Windows 11/Ubuntu package smoke.

> **Sprint exit artifact**  
> A verified Soulseek-enabled public-beta release candidate with an accepted superseding ADR, or a precise renewed `NO-GO` that names every remaining failed checklist row without weakening ADR-0009.

## Completion report

Report changed files, API/schema/event changes, migrations, exact validation commands and results, security/license impact, target-OS artifacts, checklist evidence, known risks and every unmet acceptance criterion.
