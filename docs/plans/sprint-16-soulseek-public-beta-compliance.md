# Sprint 16 Soulseek Public-Beta Compliance

## Goal

Close the feature and evidence gaps identified by ADR-0009 while preserving the existing Soulseek engine, local-first topology, legacy CLI/API behavior and provider no-audio policy.

## Current-state findings

- Search, explicit downloads and remote folder browse already flow through `SoulseekClientManager`, `ISoulseekClient`, the Server adapter and Desktop workflows.
- `EngineSupervisor` exposes connection/login state plus configured share counts and listen-port indicators, but configured counts are not proof that files are actually shared.
- The installed Soulseek.NET interface exposes room/public chat, upload and privilege methods/events; the local-files test client currently leaves those members unimplemented.
- Wishlist behavior, chat UX, actual share-root serving/upload visibility and privilege UX are absent from the daemon/Desktop product.
- ADR-0009 therefore blocks every public Soulseek-enabled artifact even though Sprint 15 performance, diagnostics and Windows RC gates pass.
- Public beta also lacks exact Windows 11 and Ubuntu LTS package smoke evidence.

## In scope

- Inventory and adapter boundaries for the installed Soulseek.NET feature surface.
- Persisted local wishlist entries and bounded reconnect-safe scheduling.
- Required room/private/public chat and privilege status/notification workflows.
- Explicit share-root configuration, safe remote-file projection, upload handling and visible upload state/controls.
- Additive authenticated localhost API, SignalR event and Desktop UI changes.
- Mock adapter support, migrations, OpenAPI, security tests and redacted live checklist evidence.
- Exact Windows 11 and Ubuntu LTS package smoke followed by a superseding release ADR when justified.

## Out of scope

- Replacing Soulseek.NET or rewriting the existing download engine.
- Provider playback/download, remote daemon hosting or centralized accounts.
- Automatically sharing the local library or download directories without explicit user opt-in.
- Persisting unlimited chat history or exposing private messages in diagnostics.
- Declaring public beta ready from mocked protocol evidence alone.

## Files and projects affected

- `Sockseek.Core` and tests for the single Soulseek.NET adapter boundary and local mock parity.
- `Sockseek.Domain`, `Sockseek.Application`, `Sockseek.Infrastructure` and tests for immutable models, use cases and persistence.
- `Sockseek.Api`, `Sockseek.Server`, OpenAPI and tests for authenticated contracts/events and Core adaptation.
- `Sockseek.Desktop` and tests for wishlist, chat, sharing/upload and privilege workflows.
- `docs/adr`, compliance checklist, release notes, threat model and package evidence.

## API, schema and event changes

- Add versioned `/api/v1` contracts; do not remove or reinterpret legacy routes.
- Add SQLite migrations for wishlist and only the bounded local state explicitly accepted during implementation.
- Add SignalR events with stable immutable DTOs; never expose Soulseek.NET or mutable Core objects.
- Treat share roots and chat/private data as sensitive local configuration; expose only the minimum status needed by Desktop.

## Implementation sequence

1. Produce a checked capability matrix from the installed Soulseek.NET assembly/XML docs and existing manager/mock implementations. Completed in `docs/soulseek-net-capability-matrix.md` for Soulseek.NET 10.0.2.
2. Define Domain/Application contracts and threat-model decisions for retention, share-root approval and reconnect semantics.
3. Implement wishlist vertically through persistence, adapter, API and Desktop with deterministic mock coverage. Completed with persisted bounded scheduling, authenticated API/OpenAPI contracts, Desktop controls, migration/upgrade coverage and scheduler/UI tests; live-network checklist evidence remains part of step 7.
4. Implement privilege recognition and chat vertically with bounded state and redaction tests.
5. Implement explicit sharing/upload vertically, beginning with canonical path policy and adversarial tests before network serving.
6. Complete reconnect/failure matrices, OpenAPI snapshots and compatibility tests.
7. Run the redacted live checklist and target-OS package smokes on one exact commit.
8. Update release notes and accept a superseding ADR only if every gate passes.

## Testing strategy

- Start with interface/adapter characterization tests before production behavior changes.
- Use deterministic `LocalFilesSoulseekClient` implementations for CI and a dedicated opt-in live account for protocol evidence.
- Add migration upgrade tests and bounded-retention/load tests for every persisted collection.
- Add path traversal, symlink/junction, unauthorized API and diagnostics redaction adversarial tests before enabling uploads.
- Finish each vertical feature with targeted tests, then full restore/build/test, provider-audio guard and package smoke.

## Migration and rollback

- Every schema change gets a forward EF migration and upgrade test from the current public schema.
- Feature configuration defaults disabled/empty so upgrades do not start sharing, chatting or wishlisting implicitly.
- Rollback disables new background/network workflows without deleting local music; schema rollback is not assumed.

## Security, privacy and license impact

- Share roots require explicit opt-in, canonical containment and a documented symlink/junction policy.
- Chat/private-message persistence is opt-in or tightly bounded and excluded/redacted from diagnostics by default.
- Soulseek credentials remain in `ISecretStore`; no new logs may contain passwords, auth material or private message content.
- The entire feature set remains AGPL-3.0 and provider no-audio policy remains unchanged.

## Risks and stop conditions

- Stop and write an ADR if Soulseek.NET cannot safely provide a required network feature without replacing the engine or changing process topology.
- Stop public-beta work if live behavior contradicts the installed library's apparent API capability.
- Do not advertise shares from counts alone, infer share roots from the local library, or expose a file after canonicalization fails.
- Do not supersede ADR-0009 while any required checklist row lacks exact evidence.

## Acceptance-criteria mapping

- Missing feature coverage: capability matrix plus vertical wishlist/chat/upload/privilege implementations and tests.
- Safe/truthful sharing: explicit roots, path-security tests, real advertised state and visible transfers.
- Recovery/bounds: reconnect, cancellation, retention and load tests across new workflows.
- Compatibility/security: versioned additive contracts, auth/redaction tests, full suite and provider-audio guard.
- Public-beta decision: redacted live checklist, exact Windows 11/Ubuntu artifacts, release notes and superseding ADR.
