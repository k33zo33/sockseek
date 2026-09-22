# Sprint 9 provider framework and secret store

## Goal

Build the security-first provider foundation needed before real Spotify and YouTube integrations: provider capability registry, OAuth PKCE/state coordinator, local secret-store abstraction, account lifecycle API/UI and fake provider E2E coverage, without introducing provider audio playback or storing tokens in SQLite.

## Current-state findings

- `Sockseek.Domain` already has `ExternalAccount`, `ExternalProvider`, account status and playlist import models.
- `Sockseek.Infrastructure` already has SQLite tables/entities for `ExternalAccounts`, `ExternalPlaylists`, `Playlists`, `PlaylistItems`, `TrackSources` and `ProviderSyncStates`.
- Existing `ExternalAccountStore.DeleteAsync` physically deletes account rows and detaches playlists; Sprint 9 needs a disconnect flow that deletes secrets and updates account status without losing local playlist data.
- Existing `ExternalPlaylistSnapshotStore` can upsert account references and playlist snapshots, but it accepts secret references from caller-supplied records and does not own provider authorization.
- `Sockseek.Integrations.Abstractions.ProviderCapabilities` is a small boolean record; Sprint 9 needs a capability-driven provider contract/registry that can hide unsupported account actions such as Bandcamp connect.
- No `ISecretStore`, OAuth PKCE coordinator, redaction helper or provider HTTP resilience pipeline exists yet.

## In scope

- Add provider-facing abstractions for playlist-source providers, authorization start/callback, external playlist snapshots, provider capabilities and account status.
- Add an `ISecretStore` abstraction with deterministic test implementation and Windows local implementation for the first target.
- Add OAuth PKCE/state coordination with adversarial callback tests, including state mismatch rejection.
- Add fake provider implementation for E2E import/sync tests.
- Add provider registry and provider capabilities endpoint/client/Desktop binding so UI hides unsupported account actions.
- Add account connect/disconnect lifecycle with secret deletion and account status update.
- Add redaction utilities/tests for Authorization, OAuth codes, access tokens, refresh tokens, code verifiers and client secrets.
- Add provider HTTP retry/backoff/rate-limit primitives where they can be tested without a real provider SDK.

## Out of scope

- Real Spotify, YouTube, Bandcamp or MusicBrainz network integrations.
- Provider audio playback, provider audio URLs, provider download methods or provider media streams.
- Write-back to external playlists.
- Remote daemon authentication changes.
- Broad playlist resolution/download behavior beyond fake-provider import/sync coverage.

## Files and projects affected

- `Sockseek.Integrations.Abstractions`: provider contracts, capabilities, OAuth/account DTOs and secret-store abstractions if kept provider-facing.
- `Sockseek.Application`: provider registry, account/use-case orchestration and redaction/HTTP policy helpers if shared above adapters.
- `Sockseek.Infrastructure`: Windows secret store, SQLite account store updates and possible EF migration if account fields need expansion.
- `Sockseek.Server`: account/provider endpoints, fake-provider registration for tests/dev mode, local-only authorization callback handling and OpenAPI updates.
- `Sockseek.Api`: account/provider request/response DTOs and typed client methods.
- `Sockseek.Desktop`: Accounts UI view model/bindings that respect capability flags.
- `Sockseek.*.Tests`: OAuth adversarial tests, secret store tests, redaction tests, provider capability UI tests and fake provider E2E coverage.

## API, schema and event changes

- Add or complete `/api/v1/providers` and `/api/v1/providers/{provider}/capabilities` with account-connect capability flags.
- Add `/api/v1/accounts`, authorize callback/start endpoints and disconnect endpoint if missing or incomplete.
- Keep tokens out of all API responses; expose only provider id, display name, public external user id, status and timestamps.
- Prefer no schema migration for the first slice because `ExternalAccounts.SecretReference` already exists. Add a migration only if current status/reference fields are insufficient for disconnect/expired semantics.
- Update `docs/openapi.json` in the same change as contract additions.

## Implementation sequence

1. Add capability model/registry and provider contract foundation, including fake/Bandcamp/MusicBrainz capability entries that prove unsupported Connect actions are hidden.
2. Add `ISecretStore` plus in-memory/test store and Windows implementation; add tests proving secret values do not persist in SQLite.
3. Add OAuth PKCE/state coordinator with callback validation and adversarial tests.
4. Add redaction helper and provider HTTP pipeline tests for sensitive fields and retry/429/backoff behavior.
5. Add account authorize/disconnect lifecycle through server API and account store updates; disconnect deletes secret and marks status disconnected.
6. Add fake provider import/sync E2E path using existing `ExternalPlaylistSnapshotStore`.
7. Add Desktop Accounts UI state/actions driven by provider capabilities.
8. Run full validation and update sprint/project documentation after all acceptance criteria pass.

## Testing strategy

- Unit tests for capability registry and provider contract invariants.
- OAuth callback adversarial tests: wrong state, missing code, reused state, expired state and redirect/provider mismatch.
- Secret store tests: set/get/delete, missing secret, idempotent delete and SQLite scans proving token payloads are absent.
- Redaction tests for Authorization headers and OAuth/token field names.
- Provider HTTP policy tests for 429/retry-after and bounded retry behavior.
- Server tests for account authorize/disconnect DTOs and OpenAPI-safe responses.
- Desktop tests proving Connect is hidden for Bandcamp and shown only for providers with `ConnectAccount`.
- Full `dotnet build -c Release` and `dotnet test -c Release --no-build`.

## Migration and rollback

- Avoid schema change until a specific gap is proven.
- If a migration is needed, include EF migration files and an upgrade test from the previous snapshot in the same chunk.
- Rollback path is disabling provider registration and leaving existing playlist/account tables unchanged; local secrets should be deleted only through explicit disconnect.

## Security, privacy and license impact

- Access tokens, refresh tokens, OAuth codes, code verifiers, client secrets and full Authorization headers must never be logged or stored in SQLite.
- SQLite may store only opaque secret references.
- OAuth uses system browser, loopback callback, PKCE and state.
- Desktop receives account status/capabilities only, never token material.
- No external provider becomes an audio source; AGPL-3.0 posture is unchanged.

## Risks and stop conditions

- Stop and request an ADR if provider work requires provider audio playback/download or arbitrary provider media URLs.
- Stop before adding non-local secret persistence or cloud storage.
- Stop before destructive account/playlist deletion semantics that could remove local music or local playlist history.
- Treat token leakage into SQLite, logs or API responses as a release blocker.

## Acceptance-criteria mapping

- Access and refresh tokens absent from SQLite/logs: covered by secret-store/SQLite scan tests and redaction tests.
- PKCE state mismatch rejected: covered by OAuth adversarial tests.
- Fake provider imports and syncs playlist: covered by fake provider E2E server/infrastructure tests.
- Disconnect deletes secret and updates account status: covered by account lifecycle tests.
- Bandcamp does not show Connect account: covered by capability registry and Desktop UI tests.
