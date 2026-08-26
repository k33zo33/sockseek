# ADR-0003: External providers are playlist and metadata sources only

## Status
Accepted

## Context
The application connects to Spotify and YouTube accounts, imports public Bandcamp URLs and uses MusicBrainz metadata. The internal product is a local/Soulseek player and downloader.

## Decision
External providers may supply account identity, playlists, playlist items, public URLs, artwork references and metadata. They do not supply audio to the internal player and are not download sources.

## Consequences
- No external-provider playback controls.
- No `IPlaybackProvider`, provider audio URL or provider download method.
- No Spotify Web Playback SDK or hidden YouTube player.
- Playlist items must resolve to a local file or Soulseek workflow before internal playback.
- UI labels actions as import, sync, resolve or open original source - never play/download from provider.
