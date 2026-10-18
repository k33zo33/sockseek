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
