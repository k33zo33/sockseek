# API and Client integration

> [!WARNING]
> This document describes the **current Sockseek daemon HTTP/SignalR API and client integration surface**.
>
> For the planned future **application API** used by Sockseek UI, see [`application-api.md`](application-api.md).
>
> The API is experimental and has not yet been tested much. Expect bugs and breaking changes.

The daemon exposes an HTTP API for durable state plus a SignalR hub for live invalidation/progress events.

## Soulseek wishlist

The authenticated versioned API exposes local persisted wishlist searches:

- `GET /api/v1/soulseek/wishlist`
- `POST /api/v1/soulseek/wishlist`
- `PUT /api/v1/soulseek/wishlist/{id}`
- `DELETE /api/v1/soulseek/wishlist/{id}`
- `POST /api/v1/soulseek/wishlist/{id}/run`

Intervals are bounded from 5 minutes through 7 days. The daemon runs at most four
wishlist searches concurrently and never starts two active searches for the same
entry. Restart recovery marks interrupted work with a stable error code and makes
an enabled entry eligible for a new search. The API stores only the query, schedule,
engine ids, result count and stable state/error code; it does not persist raw search
results or exception text in the wishlist table.

## Soulseek privileges

The authenticated versioned API exposes current privilege recognition and explicit
notification acknowledgement:

- `GET /api/v1/soulseek/privileges`
- `POST /api/v1/soulseek/privileges/notifications/{notificationId}/acknowledge`

The daemon disables Soulseek.NET automatic privilege acknowledgement. It retains at
most 100 pending notifications in memory, deduplicates replayed acknowledgement ids
and exposes opaque local notification ids instead of protocol ids. The global
`soulseek.privilege-state.changed` SignalR event invalidates the HTTP snapshot but
does not include the notifying username.

## Soulseek sharing

The authenticated versioned API exposes explicit local share-root configuration
and the active immutable share snapshot:

- `GET /api/v1/soulseek/sharing`
- `POST /api/v1/soulseek/sharing/roots`
- `PUT /api/v1/soulseek/sharing/roots/{id}`
- `DELETE /api/v1/soulseek/sharing/roots/{id}`
- `POST /api/v1/soulseek/sharing/rebuild`

Share roots are separate from library/download roots and are never inferred. The
snapshot contains supported completed audio files only, exposes stable virtual
Soulseek paths to peers and reports per-root `ready`, `disabled` or stable failure
status codes to the local Desktop client. A successful configuration mutation
invalidates the previous snapshot before rebuilding, so cancellation or scan
failure cannot leave a removed/disabled root served. Deleting configuration never
deletes physical audio files. Local paths are returned only by this protected
loopback API and never appear in Soulseek responses or SignalR events.

## Soulseek uploads

The authenticated versioned API exposes bounded current and recently completed
upload state:

- `GET /api/v1/soulseek/uploads`

The snapshot reports active, terminal and truncated counts and returns at most 200
rows. Terminal history is held in memory only and is limited to the latest 100
transfers. Rows contain an opaque local id, peer username, shared-file basename,
state, byte counts, speed and timestamps; local paths, remote endpoints and
exception details are never included. The global
`soulseek.upload-state.changed` SignalR event carries only a revision and stable
reason. Progress invalidations are coalesced to avoid flooding clients.

## Soulseek chat

The authenticated versioned API exposes bounded in-memory private, room and public
chat state plus explicit user commands:

- `GET /api/v1/soulseek/chat`
- `POST /api/v1/soulseek/chat/rooms/refresh`
- `POST /api/v1/soulseek/chat/rooms/join`
- `POST /api/v1/soulseek/chat/rooms/leave`
- `POST /api/v1/soulseek/chat/private/messages`
- `POST /api/v1/soulseek/chat/rooms/messages`
- `POST /api/v1/soulseek/chat/public/start`
- `POST /api/v1/soulseek/chat/public/stop`

The daemon retains at most 500 messages and 20 joined-room intents in memory; it
does not persist chat content. Private-message protocol ids remain internal and
replayed messages are deduplicated while retained. The Core adapter explicitly
acknowledges receipt after handing the message to local subscribers, preserving
legacy CLI behavior while package auto-acknowledgement stays disabled. Joined rooms
and public-chat opt-in are restored after a Soulseek reconnect. The global
`soulseek.chat-state.changed` SignalR event carries only revision and reason fields;
clients reload message content from the protected HTTP snapshot.

## .NET clients
.NET consumers should prefer `Sockseek.Api.SockseekApiClient` over hand-written endpoint calls.

## OpenAPI

OpenAPI spec is in `docs/openapi.json` (auto-generated during build). The same document is also served by a running daemon at `GET /api/openapi.json`.

If you are not using .NET, use the OpenAPI document with your viewer or client generator of choice.

## Local mock daemon

For client development, start from mock files instead of a real Soulseek account:

```bash
python scripts/create_mock_music_library.py -o /tmp/sockseek-fixture

sockseek daemon \
  --mock-files-dir /tmp/sockseek-fixture/mock-library \
  --mock-files-no-read-tags \
  --mock-files-slow \
  --server-port 5030 \
  -o /tmp/sockseek-out
```

## Source map

- `Sockseek.Api/Client/SockseekApiClient.cs` — .NET client wrapper and the most convenient reference for supported client flows.
- `Sockseek.Api/Contracts/` — request/response DTOs shared by the server, CLI, and .NET clients.
- `Sockseek.Api/Contracts/ServerEvents.cs` — SignalR event envelope and event payload DTOs.
- `Sockseek.Api/Client/ServerEventPayloadConverter.cs` — typed event payload rehydration for .NET clients.
- `Sockseek.Server/ServerHost.cs` — endpoint registration and OpenAPI metadata.
- `Sockseek.Cli/Services/RemoteCliBackend.cs` — real remote client usage, including SignalR subscription behavior.
- `Sockseek.Cli.Tests/RemoteCliBackendTests.cs` — executable examples of remote API flows.
