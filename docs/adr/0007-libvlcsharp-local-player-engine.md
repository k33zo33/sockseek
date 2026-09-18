# ADR-0007: Use LibVLCSharp for the local player engine

## Status
Accepted

## Context
Sprint 7 requires a stable local player MVP for local library files and completed Soulseek downloads. The player must support MP3, FLAC, Ogg/Vorbis, Opus, WAV and M4A where the selected engine supports them, expose transport controls through a coordinator, survive bad files, and never use Spotify, YouTube, Bandcamp, MusicBrainz or any other provider as an audio source.

The desktop shell is Avalonia and the playback runtime belongs behind the daemon/player boundary. The project needs a codec-capable local engine with .NET bindings and Windows support first, without rewriting codec handling in managed code.

Current research:

- LibVLCSharp 3.10.1 is the current VideoLAN .NET wrapper package and targets modern .NET-compatible platforms.
- LibVLCSharp.Avalonia 3.10.1 is available for Avalonia UI integration, but Sprint 7 playback should keep UI controls separate from the engine wrapper until the bottom player/queue surface needs video/control embedding.
- VideoLAN provides native LibVLC packages such as `VideoLAN.LibVLC.Windows`.
- LibVLCSharp is LGPL-2.1-or-later. That is compatible with this AGPL-3.0 product when third-party notices, license text and native-library redistribution obligations are preserved.

Sources:

- https://www.nuget.org/packages/LibVLCSharp/
- https://www.nuget.org/packages/LibVLCSharp.Avalonia/
- https://github.com/videolan/libvlcsharp

## Decision
Use LibVLCSharp 3.10.x as the local player engine wrapper for Sprint 7, with platform native LibVLC packages for packaged targets. The initial implementation targets Windows with `VideoLAN.LibVLC.Windows`; other platform runtimes can be added as packaging work expands.

Introduce an `IMediaEngine` abstraction in the player layer. Its source-opening API must accept only validated local file paths or explicitly modeled progressive local download files. It must not accept arbitrary provider URLs, HTTP URLs, provider stream handles, or provider SDK objects.

Keep playback source selection separate from the media engine:

- `IPlaybackSourceResolver` chooses local media files from persisted library/download state.
- `PlaybackCoordinator` owns state, queue commands and error transitions.
- `IMediaEngine` only loads and controls a supplied local source.

## Consequences
- The player can rely on mature native codec support instead of implementing codec handling in managed code.
- Public binary releases must ship/update third-party notices for LibVLCSharp, native LibVLC packages and their license obligations.
- Codec support still requires a fixture matrix because actual decode behavior depends on native LibVLC/runtime packaging.
- The engine wrapper must be tested behind fakes for state-machine behavior, with separate integration/smoke tests for real codecs.
- LibVLC features that can open network streams are intentionally not exposed through Sockseek contracts.
- If LibVLCSharp or native LibVLC packaging proves unsuitable for a target platform, a future ADR must supersede this decision.
