# Sprint 4 daemon startup handshake foundation

## Goal

Add the smallest safe Sprint 4 server-side foundation so the desktop supervisor can obtain the local daemon base URL and session token through an explicit startup handshake.

## Current-state findings

- `Sockseek.Desktop` already contains handshake parsing, a daemon supervisor abstraction, backend status state, and tests for accepted handshake payloads.
- `Sockseek.Server` already binds to loopback by default and protects `/api/v1` with a local session token.
- The current server startup path does not emit the handshake payload that the desktop supervisor expects.
- Emitting the session token through ordinary daemon logs would violate the repo security guidance, so handshake output should be isolated and explicit.

## In scope

- Add an opt-in startup handshake emitter for desktop-supervised daemon launches.
- Emit the resolved loopback base URL and session token in the desktop parser’s expected payload format.
- Cover the emitter with focused server tests.

## Out of scope

- Full desktop process launching.
- SignalR reconnect manager.
- Single-instance desktop enforcement.
- Theme persistence or command palette UI.
- Broader packaging/runtime distribution changes.

## Files and projects affected

- `Sockseek.Server`
- `Sockseek.Server.Tests`
- `docs/plans/sprint-04-daemon-startup-handshake.md`

## API, schema and event changes

- No HTTP contract changes.
- No database or migration changes.
- Adds an internal startup stdout handshake for desktop-supervised launches only.

## Implementation sequence

1. Add a small server-side handshake emitter abstraction/helper.
2. Gate emission behind an explicit environment variable so normal daemon runs do not print the token.
3. Resolve the actual listen URL after startup and serialize the handshake payload with the existing desktop field names.
4. Add focused tests for enabled/disabled emission and payload normalization.

## Testing strategy

- Targeted `Sockseek.Server.Tests` coverage for handshake emission behavior.
- Relevant desktop handshake/parser tests remain as regression coverage for payload compatibility.
- Run targeted and/or full Release test validation after implementation.

## Migration and rollback

- No migration.
- Rollback is a normal code revert; no persisted state changes.

## Security, privacy and license impact

- Keeps the daemon loopback-only by default.
- Avoids exposing session tokens in ordinary logs by requiring explicit opt-in for handshake emission.
- No license impact.

## Risks and stop conditions

- Stop if the only viable implementation would expose the session token in routine logs or non-loopback endpoints.
- Stop if startup emission requires changing a locked architecture decision.

## Acceptance-criteria mapping

- Contributes to “user does not have to manually start daemon” by enabling desktop-supervised bootstrap.
- Contributes to “secure session handshake”.
- Preserves legacy API behavior because no HTTP endpoints change.
