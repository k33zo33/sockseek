# Application API and events

> [!NOTE]
> This document describes the **planned Sockseek UI application API** and event model for future sprint work.
>
> For the **current** daemon/client integration API that exists in the repository today, see [`api.md`](api.md).

## 13. API i događaji

Postojeći /api/jobs i /api/workflows endpointovi ostaju radi kompatibilnosti. Novi UI koristi verzionirani application API pod /api/v1. Backend se po defaultu veže samo na 127.0.0.1 i zahtijeva session token koji desktop proces dobiva pri pokretanju daemona.

### 13.1. Glavni endpointi

| Područje | Endpointi |
| --- | --- |
| System | • GET /api/v1/system/info<br>• GET /api/v1/system/health<br>• GET /api/v1/system/capabilities<br>• POST /api/v1/system/shutdown |
| Onboarding | • GET /api/v1/onboarding/state<br>• POST /api/v1/onboarding/complete<br>• POST /api/v1/onboarding/test-soulseek |
| Accounts | • GET /api/v1/accounts<br>• POST /api/v1/accounts/{provider}/authorize<br>• GET /api/v1/accounts/{provider}/callback<br>• DELETE /api/v1/accounts/{id} |
| Providers | • GET /api/v1/providers<br>• GET /api/v1/providers/{provider}/capabilities |
| Playlists | • GET /api/v1/playlists<br>• POST /api/v1/playlists/import<br>• POST /api/v1/playlists/{id}/sync<br>• POST /api/v1/playlists/{id}/resolve<br>• POST /api/v1/playlists/{id}/download |
| Playlist items | • PATCH /api/v1/playlist-items/{id}<br>• POST /api/v1/playlist-items/{id}/resolve<br>• POST /api/v1/playlist-items/{id}/choose-match<br>• POST /api/v1/playlist-items/{id}/download |
| Library | • GET /api/v1/library/tracks<br>• GET /api/v1/library/albums<br>• POST /api/v1/library/roots<br>• POST /api/v1/library/scan<br>• POST /api/v1/library/tracks/{id}/relink |
| Downloads | • GET /api/v1/downloads<br>• POST /api/v1/downloads/{id}/cancel<br>• POST /api/v1/downloads/{id}/next-candidate<br>• POST /api/v1/downloads/{id}/retry |
| Player | • GET /api/v1/player/state<br>• GET /api/v1/player/queue<br>• POST /api/v1/player/play<br>• POST /api/v1/player/pause<br>• POST /api/v1/player/seek<br>• POST /api/v1/player/queue/items |
| Settings | • GET /api/v1/settings<br>• PATCH /api/v1/settings<br>• GET /api/v1/profiles |

### 13.2. SignalR događaji

```text
system.backend-state-changed
provider.authorization-changed
provider.sync-progress
playlist.sync-completed
playlist.item-state-changed
playlist.resolution-progress
library.scan-progress
library.file-changed
search.results-updated
download.state-changed
download.progress
player.state-changed
player.position-changed
player.buffer-changed
notification.created
```

Svaki event envelope mora sadržavati EventId, EventType, OccurredAtUtc, CorrelationId, WorkflowId nullable, EntityId nullable, Sequence i Payload. Klijent mora moći nakon reconnecta dohvatiti autoritativni snapshot; eventovi nisu jedini source of truth.
