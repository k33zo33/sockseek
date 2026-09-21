# Sprint 8 progressive playback foundation

## Goal

Add the first experimental, feature-flagged play-while-downloading foundation so the player can reason about a growing local Soulseek `.incomplete` file, buffer readiness and progressive codec eligibility without introducing provider audio or indexing incomplete files as final library media.

## Current-State Findings

- Sprint 7 player supports local completed-file playback through `PlaybackCoordinator`, `IMediaEngine`, `LocalPlaybackSourceResolver` and the `/api/v1/player` endpoints.
- `EngineSupervisor.GetSystemCapabilities()` exposes player codec capabilities, but `ProgressivePlayback` is currently false globally and per codec.
- `Downloader.DownloadFile()` writes to `<output>.incomplete` unless `TransferSettings.NoIncompleteExt` is true, raises download progress/state events and renames to the final output path on success.
- The persistence model already has `PlaybackQueueItemState.ProgressiveDownload`, but no progressive media source, buffer state, seek limit or active-download resolver exists.
- `LocalLibraryScanner` indexes completed files from configured library roots; Sprint 8 must keep `.incomplete` files out of final library indexing.

## In Scope

- Add player-layer models for progressive local media sources and buffer state.
- Add a buffer policy that calculates initial readiness, underrun and seekable range from local file length, expected bytes, bitrate/duration hints and feature flags.
- Add codec capability reporting for progressive playback behind an experimental feature flag, starting with MP3 only after a growing-file fixture proves LibVLC can open it.
- Add controlled tests for slow growth, underrun/resume, cancel/candidate switch cleanup behavior at the coordinator/policy boundary.
- Wire server capabilities and player state DTOs only where the existing API shape can safely represent the new state; add DTO fields only if needed and update OpenAPI in the same change.

## Out of Scope

- Provider streaming, provider URLs or provider SDK playback.
- Spotify, YouTube, Bandcamp or MusicBrainz audio download/playback.
- Indexing `.incomplete` files as durable `LocalMediaFile` records.
- Non-Windows OS media session work.
- Broad downloader rewrite beyond exposing the active local incomplete path/progress needed by the player.

## Files And Projects Affected

- `Sockseek.Player`: progressive source/buffer models, policy/coordinator behavior and tests.
- `Sockseek.Server`: capability feature flag, active download progress adapter and API state mapping.
- `Sockseek.Api`: DTO/client updates if buffer state must be exposed.
- `Sockseek.Core`: minimal download event/snapshot exposure if the server cannot currently locate the active `.incomplete` path safely.
- `Sockseek.Infrastructure`: only if scanner exclusion needs hardening tests for `.incomplete` files.
- `docs`: sprint status, capability notes and completion report.

## API, Schema And Event Changes

- Prefer no database schema change in the first slice.
- If client-visible buffer state is added, extend existing player DTOs with nullable/backward-compatible fields and regenerate `docs/openapi.json`.
- SignalR `player.buffer-changed` is planned but should be added only with authoritative snapshot support.

## Implementation Sequence

1. Add tests and player models for `ProgressiveMediaSource`, `PlaybackBufferState` and a deterministic `ProgressiveBufferPolicy`.
2. Add a feature flag in server options for progressive playback; keep it disabled by default.
3. Run a growing-file MP3 fixture spike against LibVLC and update the codec capability report only for formats with evidence.
4. Add server-side active download snapshot plumbing for incomplete local file path, expected size and progress speed without exposing provider URLs.
5. Add coordinator behavior for initial buffer, underrun/resume and cancel/switch cleanup using a fake media engine first.
6. Add API/buffer DTO exposure and Desktop buffering UI only after the authoritative state model is stable.

## Testing Strategy

- Player unit tests for initial buffer threshold, seek range, underrun and resume.
- Codec-specific growing-file regression tests for MP3 before advertising support.
- Server tests for feature flag capability reporting and active progressive source mapping.
- Scanner tests proving `.incomplete` files are skipped or remain non-final.
- Full `dotnet build -c Release` and `dotnet test -c Release --no-build`.

## Migration And Rollback

- No migration planned for the foundation slice.
- Rollback disables the feature flag, removes progressive source mapping and keeps completed local playback behavior from Sprint 7.

## Security, Privacy And License Impact

- The player opens only local `.incomplete` files created by the Soulseek downloader.
- No provider audio URL, arbitrary HTTP URL, provider SDK stream or external audio source is introduced.
- Local paths remain exposed only through authenticated local `/api/v1` player state, matching existing Sprint 7 behavior.
- AGPL-3.0 posture is unchanged.

## Risks And Stop Conditions

- Stop and request an ADR if implementation requires changing the audio-source policy or accepting arbitrary URLs.
- Stop if LibVLC cannot safely open growing MP3 files with deterministic tests.
- Stop before schema changes unless a migration and upgrade test are added in the same chunk.
- Treat `.incomplete` library indexing as a release blocker for Sprint 8.

## Acceptance-Criteria Mapping

- Supported MP3 fixture starts before download completion: covered by growing-file MP3 regression test before enabling MP3 progressive capability.
- Slow download enters Buffering and resumes: covered by buffer policy/coordinator underrun tests.
- Unsupported format waits for complete: covered by codec capability/policy tests.
- Cancel stops playback and cleans temporary source: covered by cancel/switch tests.
- No incomplete library indexing: covered by scanner exclusion tests.
