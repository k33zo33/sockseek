# Sprint 7 - Player control API

## Goal

Expose the local player state and core playback controls through authenticated versioned daemon endpoints.

## Current-state findings

- `PlaybackCoordinator` is process-scoped and owns playback state, queue state and commands.
- `/api/v1` endpoints already require the local session token except health.
- Desktop communicates with the daemon over localhost HTTP and SignalR; player UI needs daemon controls next.

## In scope

- Add DTOs for player state, queue state and basic command requests.
- Add authenticated `/api/v1/player` endpoints for state, play, pause, resume, stop, next, previous, seek, volume and mute.
- Update source-generated JSON metadata, OpenAPI and server tests.

## Out of scope

- Desktop bottom player UI.
- Persisted queue hydration API.
- SignalR now-playing events.
- Media keys and OS media session bridge.
- Provider audio URLs or external audio downloads.

## Files and projects affected

- `Sockseek.Api`
- `Sockseek.Server`
- `Sockseek.Server.Tests`
- `docs/openapi.json`
- `docs/plans`

## API, schema and event changes

- Adds `/api/v1/player/*` HTTP endpoints under existing local session-token protection.
- No database schema or SignalR event changes.

## Implementation sequence

1. Add player DTOs and JSON source-generation metadata.
2. Map player endpoints in `ServerHost`.
3. Add conversion helpers from Player snapshots to API DTOs.
4. Add server endpoint and OpenAPI tests.
5. Run full validation.

## Testing strategy

- Endpoint tests cover auth, state response and basic command response.
- OpenAPI tests verify player paths and DTO schemas.
- Full Release build and test suite before commit.

## Migration and rollback

- No migration.
- Rollback removes endpoint mappings and DTOs.

## Security, privacy and license impact

- Endpoints are under `/api/v1` and require the local session token.
- Responses can expose local file paths only to authenticated local clients, matching existing library endpoints.
- No provider playback or audio download capability is introduced.

## Risks and stop conditions

- Stop if the endpoint design requires playing provider URLs or adding provider stream/download methods.
- Stop if command handling would require bypassing local session-token middleware.

## Acceptance-criteria mapping

- Moves Sprint 7 toward bottom player and expanded queue integration.
- Does not complete Desktop UI, media keys, codec fixtures or queue restore acceptance criteria.
