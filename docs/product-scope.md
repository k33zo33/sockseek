# Product scope

Sockseek UI extends the existing Sockseek fork into a local-first desktop music manager, Soulseek downloader, and local audio player.

## Product definition

- Desktop application with a separate local daemon over localhost HTTP/SignalR
- Local library management and local playback
- Soulseek search, candidate review, download, retry, and progressive playback where supported
- External services used only for playlist import or metadata enrichment

## Allowed sources of playback audio

Playback inside the product may use only:

- a local library file
- a completed Soulseek download
- a supported progressive Soulseek download

## Explicitly out of scope

The product is **not**:

- a Spotify player
- a YouTube player
- a Bandcamp streaming client
- a MusicBrainz playback client
- a YouTube/Spotify audio downloader
- a centralized cloud Soulseek service
- a big-bang rewrite of the Sockseek engine

## Locked implementation boundaries

- Continue from the existing Sockseek fork
- Keep the full product under GNU AGPL-3.0
- Keep legacy CLI and daemon behavior working unless a later ADR deprecates them
- Keep the application local-first: music, database, Soulseek credentials, and provider tokens stay on the user's machine
- Keep UI/application layers isolated from mutable `Sockseek.Core` job objects

## Provider rule

External providers may supply:

- account identity
- playlists and playlist items
- public URLs
- artwork references
- metadata

External providers may **not** supply:

- internal playback audio
- provider audio URLs
- provider-side download methods
- hidden streaming players

## Release boundary

No public release should claim or imply playback or downloading from Spotify, YouTube, Bandcamp, or MusicBrainz.
