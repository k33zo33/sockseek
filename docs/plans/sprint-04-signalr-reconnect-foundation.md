# Sprint 4 SignalR reconnect foundation

## Goal

Add the smallest desktop-side SignalR connection and reconnect manager foundation needed for the Sprint 4 daemon-supervised shell.

## Current-state findings

- `Sockseek.Desktop` already has daemon launch supervision, secure startup handshake parsing, and authenticated HTTP client construction.
- The server already exposes a SignalR hub at `/api/events` with `SubscribeAll` and workflow-scoped subscription methods.
- The repo already contains shared SignalR DTOs plus `ServerEventPayloadConverter` and `WorkflowClientStore` helpers in `Sockseek.Api`.
- The desktop project does not yet have a reusable event connection factory or reconnect manager.

## In scope

- Add a desktop event hub connection abstraction for testability.
- Add a concrete SignalR-backed factory using the daemon handshake.
- Add a reconnect manager that tracks connected/reconnecting/disconnected state and rehydrates incoming event payloads.
- Add focused unit tests for manager state transitions and subscription calls.

## Out of scope

- Full UI binding to live events.
- Snapshot recovery on sequence gaps.
- Workflow-specific view models.
- Full Avalonia bootstrap.
- Server-side SignalR auth changes.

## Files and projects affected

- `Sockseek.Desktop`
- `Sockseek.Desktop.Tests`
- `docs/plans/sprint-04-signalr-reconnect-foundation.md`

## API, schema and event changes

- No server API contract changes.
- No database or migration changes.
- Desktop-only event connection and reconnect handling.

## Implementation sequence

1. Add a small desktop event-hub connection abstraction.
2. Add a SignalR-backed connection wrapper and handshake-based factory.
3. Add a reconnect manager that surfaces connection-state changes and typed event callbacks.
4. Add unit tests for start/stop, subscribe, reconnect, reconnect success, close, and payload rehydration.
5. Run targeted desktop tests.

## Testing strategy

- Targeted `Sockseek.Desktop.Tests` run in Docker.
- Fake connection tests for reconnect manager behavior.
- Reuse shared API DTO/payload conversion helpers to validate typed event handling.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- Uses the existing loopback daemon URI and session token handshake.
- No new credential storage or logging.
- No license impact.

## Risks and stop conditions

- Stop if the manager needs to own UI-thread concerns at this layer.
- Stop if the only viable design tightly couples tests to `HubConnection` internals.
- Stop if the client must depend on mutable server/runtime types outside shared DTO contracts.

## Acceptance-criteria mapping

- Contributes to “implement API client and SignalR reconnect manager”.
- Supports recovery after controlled daemon restarts.
- Keeps desktop dependencies within API contracts/client and UI-friendly abstractions.
