# Sprint 16 Soulseek Public-Beta Compliance

## Goal

Close the feature and evidence gaps identified by ADR-0009 while preserving the existing Soulseek engine, local-first topology, legacy CLI/API behavior and provider no-audio policy.

## Current-state findings

- Search, explicit downloads and remote folder browse already flow through `SoulseekClientManager`, `ISoulseekClient`, the Server adapter and Desktop workflows.
- `EngineSupervisor` exposes connection/login state plus counts from the active immutable share snapshot and listen-port indicators; legacy configured counts remain only when no share source is installed.
- The packaged Desktop starts `Sockseek.Server` directly and currently has no user-facing way to configure Soulseek credentials. Environment-bound credentials are possible, but do not satisfy the local secret-store or packaged onboarding requirements.
- The default secret-store registration is Windows DPAPI-only even though Ubuntu LTS is a release target; the packaged Linux daemon therefore needs a local encrypted secret-store implementation before Soulseek account onboarding can be complete.
- The installed Soulseek.NET interface exposes room/public chat, upload and privilege methods/events; privilege, chat and stream-based upload parity are implemented in the local-files client.
- Wishlist, privilege and chat workflows are implemented through the daemon and Desktop with bounded state and deterministic automated coverage. Share-root serving and incoming upload enqueueing are connected to Soulseek.NET; authenticated API/client and Desktop root CRUD, per-root status, manual rebuild and bounded upload visibility are implemented. Redacted live verification remains absent.
- ADR-0009 therefore blocks every public Soulseek-enabled artifact even though Sprint 15 performance, diagnostics and Windows RC gates pass.
- Ubuntu 24.04 x64 exact-package evidence, Windows 10 Pro package/runtime preflight and Docker/headless smoke pass on candidate `c7b4cc5`, including packaged secret-backed account restart/restore checks. Full local validation passes 1,584 tests and GitHub Actions run `39661044471` is green. The required Windows 11 x64 run remains unavailable.

## In scope

- Inventory and adapter boundaries for the installed Soulseek.NET feature surface.
- Persisted local wishlist entries and bounded reconnect-safe scheduling.
- Required room/private/public chat and privilege status/notification workflows.
- Explicit share-root configuration, safe remote-file projection, upload handling and visible upload state/controls.
- Additive authenticated localhost API, SignalR event and Desktop UI changes.
- Mock adapter support, migrations, OpenAPI, security tests and redacted live checklist evidence.
- Exact Windows 11 and Ubuntu LTS package smoke followed by a superseding release ADR when justified.
- Secret-backed Soulseek account configuration for packaged Desktop on Windows and Ubuntu, including reconnect-safe daemon activation without exposing the password through API responses, SQLite or diagnostics.

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
- `Sockseek.Infrastructure` persistence/security and tests for an opaque Soulseek secret reference plus a cross-platform encrypted local secret store.
- `docs/adr`, compliance checklist, release notes, threat model and package evidence.

## API, schema and event changes

- Add versioned `/api/v1` contracts; do not remove or reinterpret legacy routes.
- Add SQLite migrations for wishlist and only the bounded local state explicitly accepted during implementation.
- Add SignalR events with stable immutable DTOs; never expose Soulseek.NET or mutable Core objects.
- Treat share roots and chat/private data as sensitive local configuration; expose only the minimum status needed by Desktop.
- Add authenticated `GET`, `PUT` and `DELETE /api/v1/soulseek/account` contracts. Responses expose username, configuration/connection state and update time only; the request password is written directly to `ISecretStore` and is never returned.
- Add a singleton Soulseek account table containing username, opaque secret reference and timestamps. No password or secret payload is stored in SQLite.

## Implementation sequence

1. Produce a checked capability matrix from the installed Soulseek.NET assembly/XML docs and existing manager/mock implementations. Completed in `docs/soulseek-net-capability-matrix.md` for Soulseek.NET 10.0.2.
2. Define Domain/Application contracts and threat-model decisions for retention, share-root approval and reconnect semantics.
3. Implement wishlist vertically through persistence, adapter, API and Desktop with deterministic mock coverage. Completed with persisted bounded scheduling, authenticated API/OpenAPI contracts, Desktop controls, migration/upgrade coverage and scheduler/UI tests; live-network checklist evidence remains part of step 7.
4. Implement privilege recognition and chat vertically with bounded state and redaction tests. Automated implementation is complete through Core, daemon/API and Desktop. Privileges provide manual acknowledgement, bounded replay-safe state, username-free invalidation and HTTP snapshot recovery. Chat provides deterministic private/room/public parity, explicit adapter receipt acknowledgement, 500-message/20-room bounds, bounded virtualized Desktop lists, explicit public-chat opt-in, replay dedupe, content-free invalidation, coalesced HTTP snapshot recovery and reconnect restoration. Redacted live verification remains in step 7.
5. Implement explicit sharing/upload vertically, beginning with canonical path policy and adversarial tests before network serving. Automated implementation is complete: explicit SQLite-backed roots, immutable audio-only snapshots, virtual remote paths, reparse-point exclusion and pre-open canonical/path/size revalidation have migration and adversarial coverage. Soulseek.NET incoming search/browse/directory/upload resolvers use the snapshot, enforce one upload slot and publish serialized snapshot-derived counts on login/rebuild. Authenticated API/client and Desktop root CRUD, per-root indexing status, fail-closed invalidation and manual rebuild are implemented. Upload visibility includes active state, 100 retained terminal summaries, a 200-row/truncation contract, coalesced content-free SignalR invalidation, basename-only redaction, stream-based mock parity and a bounded virtualized Desktop activity list with HTTP snapshot recovery. Reconnect, failure, cancellation, retained-terminal and invalidation-coalescing tests pass. Live evidence remains in step 7.
6. Complete reconnect/failure matrices, OpenAPI snapshots and compatibility tests. Completed for automated Sprint 16 surfaces, including upload reconnect subscription stability, terminal failure redaction, cancellation cleanup, Desktop follow-up snapshot coalescing, generated OpenAPI and full Core/Server/Desktop regression suites. Exact-artifact live and package verification remains in step 7.
7. Implement packaged Soulseek account onboarding through `ISecretStore`, an opaque SQLite reference, authenticated API contracts and Desktop controls. Completed with cross-platform encrypted persistence, migration/upgrade coverage, authenticated API/client contracts, startup restore, idle-engine restart and Desktop configure/disconnect/recovery controls. Explicit legacy CLI credentials retain startup precedence, and active/queued jobs reject account changes.
8. Run the redacted live checklist and target-OS package smokes on one exact commit. In progress: candidate `c7b4cc5` passes full restore/build/test (1,584 tests), provider-audio, dependency and release/compliance gates, GitHub Actions run `39661044471`, Docker/headless smoke, Ubuntu 24.04 x64 exact-package smoke and Windows exact-package preflight on Windows 10 Pro. Both package smokes exercise secret-backed account configure, daemon restart/restore and disconnect, including Linux at-rest permissions and cleanup. The Windows helper records authoritative host metadata so that Windows 10 preflight cannot be mistaken for Windows 11 evidence. Artifact-bound live-checklist generator/validator scripts now capture embedded commit, package SHA256, host classification, UTC observations and redacted evidence while rejecting incomplete rows and common credential forms; their smoke plus the compliance gate run in CI when relevant scripts or decision documents change. Exact-candidate Windows 11 x64 execution and the dedicated-account redacted live Soulseek checklist remain.
9. Update release notes and accept a superseding ADR only if every gate passes.

## Testing strategy

- Start with interface/adapter characterization tests before production behavior changes.
- Use deterministic `LocalFilesSoulseekClient` implementations for CI and a dedicated opt-in live account for protocol evidence.
- Add migration upgrade tests and bounded-retention/load tests for every persisted collection.
- Add path traversal, symlink/junction, unauthorized API and diagnostics redaction adversarial tests before enabling uploads.
- Add encrypted-secret-at-rest, file-permission, migration-upgrade, unauthorized account API, response-redaction, engine-restart and Desktop password-clearing tests for Soulseek account onboarding.
- Finish each vertical feature with targeted tests, then full restore/build/test, provider-audio guard and package smoke.

## Migration and rollback

- Every schema change gets a forward EF migration and upgrade test from the current public schema.
- Feature configuration defaults disabled/empty so upgrades do not start sharing, chatting or wishlisting implicitly.
- Existing CLI/config credentials retain precedence for that daemon invocation. Packaged Desktop credentials are restored from the local secret store only when explicit engine credentials are absent.
- Rollback disables new background/network workflows without deleting local music; schema rollback is not assumed.

## Security, privacy and license impact

- Share roots require explicit opt-in, canonical containment and a documented symlink/junction policy.
- Chat/private-message persistence is opt-in or tightly bounded and excluded/redacted from diagnostics by default.
- Soulseek credentials remain in `ISecretStore`; no new logs may contain passwords, auth material or private message content.
- Windows uses DPAPI. Ubuntu uses an application-local AES-GCM store whose key and encrypted payloads are created with user-only filesystem permissions; neither payload nor key material enters SQLite, API responses or diagnostics.
- The entire feature set remains AGPL-3.0 and provider no-audio policy remains unchanged.

## Risks and stop conditions

- Stop and write an ADR if Soulseek.NET cannot safely provide a required network feature without replacing the engine or changing process topology.
- Stop public-beta work if live behavior contradicts the installed library's apparent API capability.
- Do not advertise shares from counts alone, infer share roots from the local library, or expose a file after canonicalization fails.
- Do not supersede ADR-0009 while any required checklist row lacks exact evidence.
- Reject credential changes while download work is active; never silently cancel a transfer to switch accounts.

## Acceptance-criteria mapping

- Missing feature coverage: capability matrix plus vertical wishlist/chat/upload/privilege implementations and tests.
- Safe/truthful sharing: explicit roots, path-security tests, real advertised state and visible transfers.
- Recovery/bounds: reconnect, cancellation, retention and load tests across new workflows.
- Compatibility/security: versioned additive contracts, auth/redaction tests, full suite and provider-audio guard.
- Packaged login readiness: secret-backed account CRUD, startup restore, idle engine reconnect and Desktop controls on both target OS families.
- Public-beta decision: redacted live checklist, exact Windows 11/Ubuntu artifacts, release notes and superseding ADR.
