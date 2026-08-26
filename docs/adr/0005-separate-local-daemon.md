# ADR-0005: Keep the backend as a separate local daemon process

## Status
Accepted

## Context
The existing server already exposes HTTP, OpenAPI and SignalR and can keep downloads alive independently of UI rendering.

## Decision
The Avalonia desktop process starts and supervises a separate ASP.NET Core daemon. Communication uses localhost HTTP and SignalR with a startup session token.

## Consequences
- UI crashes need not terminate active downloads.
- Legacy CLI and future remote-control clients can reuse the API.
- Startup handshake, restart policy and local API authentication are required.
- Release packaging includes both executables.
