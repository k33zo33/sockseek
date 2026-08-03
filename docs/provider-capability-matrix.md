# Provider capability matrix

| Provider | Import / metadata capability allowed in MVP | Explicitly disallowed | Notes |
| --- | --- | --- | --- |
| Spotify | OAuth PKCE, playlist listing, playlist item import, optional saved-tracks import | Playback, audio stream URLs, downloading audio | Account/app limits and quota messaging required |
| YouTube | OAuth, playlist listing, playlist item import, metadata ingestion | Playback, audio extraction, downloading audio | Playlist import only |
| Bandcamp | Public track/album/artist URL import, metadata extraction | Authenticated account connect, playback, downloading provider audio | MVP uses public URLs only |
| MusicBrainz | Metadata lookup, MBID/ISRC enrichment, release/recording identity | Playback, audio download, user playlist playback | Metadata provider only |
| ListenBrainz | Optional future playlist/history integration | Playback, audio download | Post-MVP / future ADR-driven |
| Soulseek | Search, browse, candidate ranking, download, retry, progressive local playback where supported | Centralized hosted service behavior | Primary audio backend |

## Contract rule

Do not add any of the following to provider contracts:

- `IPlaybackProvider`
- provider audio URL fields for the internal player
- `GetAudioStreamAsync`
- `DownloadTrackAsync`

## UI rule

Use labels such as:

- Import playlist
- Sync playlist
- Resolve tracks
- Open original source

Do not use labels such as:

- Play from Spotify
- Play from YouTube
- Download from Spotify
- Download from YouTube
