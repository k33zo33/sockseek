# Soulseek.NET capability matrix

Checked on 2026-10-18 against the installed `Soulseek` NuGet package `10.0.2`.
The package archive SHA-256 is
`2571FA6F7CCCD69678EEDD9613DEB877E06D1E41EB3961C467EA9F49D972D4C9`.
This inventory is based on the package assembly/XML documentation and the existing
`SoulseekClientManager`, `LocalFilesSoulseekClient` and server adapter. It is an
engineering inventory, not a public-beta compliance decision.

## Capability summary

| Required area | Soulseek.NET 10.0.2 surface | Current Sockseek state | Planned owner |
| --- | --- | --- | --- |
| Search | `SearchAsync`, `SearchRequestReceived`, `SearchResponseResolver` | Outbound search and result/download workflows are implemented. Incoming searches return a bounded result set from the active approved share snapshot only. | Existing Core search jobs; sharing resolver in Core. |
| Wishlist | No native wishlist API. `SearchAsync` provides the protocol operation needed for repeated searches. | Implemented through persisted bounded scheduling, the existing engine gateway, authenticated daemon API and Desktop controls. Redacted live smoke remains. | Domain model, Infrastructure store, Server scheduler, existing engine gateway and Desktop controls. |
| Download | `DownloadAsync`, `EnqueueDownloadAsync`, download/transfer events | Implemented through existing Core jobs and server adapter. | Existing Core/Server boundary. |
| Folder browse | `BrowseAsync`, `GetDirectoryContentsAsync` | Remote browse is implemented. Local browse and directory responses project only virtual paths from the active approved share snapshot. | Existing remote flow; sharing resolvers in Core. |
| Upload / sharing | `SearchResponseResolver`, `BrowseResponseResolver`, `DirectoryContentsResolver`, `EnqueueDownload`, `PlaceInQueueResolver`, `UploadAsync`, `EnqueueUploadAsync`, `Uploads`, transfer events | Explicit persisted roots feed an immutable audio-only snapshot with virtual paths and canonical/reparse/size revalidation. Core serves incoming search/browse/directory requests, enqueues requested files through one upload slot and publishes snapshot-derived counts. Authenticated API/client root CRUD, per-root status, fail-closed invalidation and manual rebuild are implemented. Desktop controls and visible bounded upload state remain absent. | Infrastructure path/index policy; Core resolver and transfer adapter; Server API/events; Desktop controls. |
| Private chat | `PrivateMessageReceived`, `SendPrivateMessageAsync`, `AcknowledgePrivateMessageAsync` | Core, daemon/API and Desktop receive/send workflows are implemented with bounded input/state, replay dedupe, explicit adapter receipt acknowledgement, content-free invalidation events and coalesced HTTP snapshot recovery. Package auto-acknowledgement is disabled. Redacted live smoke remains. | Core event adapter and commands; bounded Server memory; Desktop conversation UI. |
| Room chat | `GetRoomListAsync`, `JoinRoomAsync`, `LeaveRoomAsync`, `SendRoomMessageAsync`, room message/member/ticker events | Core, daemon/API and Desktop list/join/leave/send workflows are implemented with bounded virtualized UI, bounded room/member snapshots, 20-room joined intent and reconnect rejoin. Redacted live smoke remains. Private-room membership/moderation methods are available but not required for the initial chat workflow. | Core event adapter and commands; bounded Server state; Desktop room UI. |
| Public chat | `StartPublicChatAsync`, `StopPublicChatAsync`, `PublicChatMessageReceived` | Core, daemon/API and explicit Desktop opt-in/stop workflows are implemented with reconnect restoration and bounded state. Redacted live smoke remains. | Core adapter and explicit Desktop opt-in. |
| Privileges | `GetPrivilegesAsync`, `GetUserPrivilegedAsync`, `GrantUserPrivilegesAsync`, `PrivilegedUserListReceived`, `PrivilegeNotificationReceived`, `AcknowledgePrivilegeNotificationAsync` | Core, daemon/API and Desktop status/notification recognition plus explicit acknowledgement are implemented. Runtime state is bounded, username-free SignalR invalidations reload the authoritative HTTP snapshot and package auto-acknowledgement is disabled. Redacted live smoke remains. | Core adapter; Server status/notification state; Desktop recognition and acknowledgement. |

## Sharing resolver contract

Soulseek.NET does not build a local share index. Sockseek must supply all of these
delegates when constructing `SoulseekClientOptions`:

| Resolver | Signature | Required behavior |
| --- | --- | --- |
| Search response | `(username, token, SearchQuery) -> SearchResponse` | Return only indexed files under enabled, readable, explicitly approved roots. |
| Browse response | `(username, endpoint) -> BrowseResponse` | Project approved roots to stable virtual Soulseek paths without revealing local paths. |
| Directory contents | `(username, endpoint, token, directory) -> IEnumerable<Directory>` | Resolve a virtual directory using the same immutable share snapshot. |
| Incoming download | `(username, endpoint, remoteFilename) -> Task` | Canonicalize the virtual path, resolve it from the snapshot, then enqueue an upload. Never treat the request as a local path. |
| Queue position | `(username, endpoint, remoteFilename) -> int?` | Report a truthful bounded queue position or no position. |
| User info | `(username, endpoint) -> UserInfo` | Report configured upload slots, queue state and free-slot state without private local data. |

`SetSharedCountsAsync` only advertises directory/file counts. It is not proof that
files are available and must run only from the same successfully built share
snapshot used by the resolvers.

## Option defaults that require an explicit decision

- Listener is enabled by default on port `50000`; Sockseek currently uses the
  configured port or disables the listener when no port is configured.
- Maximum concurrent uploads defaults to `10`; Sockseek overrides it with one
  upload slot. User-visible transfer state and controls remain required.
- Private messages and privilege notifications are automatically acknowledged by
  default. Sockseek disables both package defaults: the Core adapter acknowledges
  private-message receipt after local handoff, while privilege acknowledgement is
  an explicit visible action.
- Private-room invitations default to disabled and must remain explicit opt-in.
- Incoming search deduplication defaults to enabled and should remain enabled.
- Event dispatch is synchronous by default. Event handlers must remain short and
  hand bounded immutable records to Server-owned queues.

## Reconnect and retention requirements

- Wishlist scheduling is application-owned because Soulseek.NET has no native
  wishlist primitive. One persisted entry may have at most one active search, and
  the scheduler must use a bounded interval and survive daemon restarts.
- Chat events can be replayed by the server (`PrivateMessageReceivedEventArgs.Replayed`).
  Deduplication uses the server message id where available. Message text is not
  written to logs, diagnostics or persistence; only the latest 500 messages remain
  in memory.
- Event subscriptions must be attached once per client instance and remain valid
  across reconnects. Commands must wait for `SoulseekClientManager` readiness.
- Upload state comes from `Uploads` and transfer state/progress events. Retained
  terminal transfer summaries must be bounded; file contents and full local paths
  are never copied into API events or diagnostics.
- Privilege notifications expose an optional acknowledgement id. Manual
  acknowledgement is only possible when that id is present; current-user privilege
  days come from `GetPrivilegesAsync`.

## Boundary map

| Layer | Allowed responsibility |
| --- | --- |
| `Sockseek.Domain` | Immutable wishlist/share/chat/privilege concepts and validation with BCL-only dependencies. |
| `Sockseek.Application` | Store and gateway interfaces plus immutable operation/result models; no Soulseek.NET types. |
| `Sockseek.Infrastructure` | SQLite persistence and canonical local share-root/index implementation. |
| `Sockseek.Core` | The only Soulseek.NET client options, methods, events and transfer adaptation. |
| `Sockseek.Server` | Scheduling, bounded runtime state, authenticated localhost endpoints and SignalR publication. |
| `Sockseek.Api` | Versioned immutable request/response/event DTOs and client methods. |
| `Sockseek.Desktop` | Explicit user controls and recovery UX through the API only. |

## Stop conditions

- Do not enable sharing until canonical containment, reparse-point policy and
  adversarial tests pass on supported operating systems.
- Do not advertise configured counts when the active immutable share snapshot is
  absent, stale or unreadable.
- Do not persist private-message content by default or include it in diagnostics.
- Do not claim live compliance from mock-client behavior; every checklist row still
  requires redacted live evidence on the exact release candidate.
