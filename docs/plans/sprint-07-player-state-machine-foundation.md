# Sprint 7 player state machine foundation

## Goal

Add the first local player MVP foundation: media engine abstraction, playback state snapshots, and coordinator transitions for resolving and playing local files without adding provider audio.

## Current-state findings

- `PlaybackCoordinator` currently delegates source resolution only.
- Sprint 6 added `IPlaybackSourceResolver`, which can resolve canonical tracks or playlist items to local files, pending, or unavailable states.
- ADR-0007 selects LibVLCSharp for the eventual concrete media engine, but no engine package or adapter is installed yet.

## In scope

- Add `IMediaEngine` and player state/result models.
- Extend `PlaybackCoordinator` with play, pause, resume and stop transitions.
- Keep existing resolve helper methods for current callers/tests.
- Add unit tests using a fake media engine.
- Reject non-file/provider-like URI sources before calling the media engine.

## Out of scope

- LibVLCSharp package installation and native runtime packaging.
- Queue persistence, shuffle/repeat and media keys.
- Desktop bottom player UI.
- Progressive Soulseek playback.

## Files and projects affected

- `Sockseek.Player`: player abstractions, state snapshots and coordinator behavior.
- `Sockseek.Player.Tests`: state machine unit tests.
- `docs/plans`: this plan.

## API, schema and event changes

- No daemon HTTP API change.
- No database schema change.
- No SignalR event change.
- Player-layer public API expands with local playback commands and snapshots.

## Implementation sequence

1. Add player state and media engine abstractions.
2. Extend `PlaybackCoordinator` to resolve local files and drive the engine.
3. Guard against provider/non-file URI sources.
4. Add fake-engine tests for success, pending/unavailable, provider URL rejection and engine failure.
5. Run targeted Player tests, full Release build and full test suite.

## Testing strategy

- Unit tests cover coordinator transitions and media engine call order.
- Existing playback source resolver tests remain source-selection coverage.

## Migration and rollback

- No migration required.
- Rollback removes the new player models and restores resolver-only coordinator behavior.

## Security, privacy and license impact

- Local-first only; media engine commands receive local paths from the resolver.
- Provider URLs are rejected before engine load.
- No external provider SDK playback contract is introduced.

## Risks and stop conditions

- Stop for ADR if implementation would require provider audio, arbitrary URL playback or a different engine choice.
- Keep actual LibVLC/native packaging for a later Sprint 7 slice.

## Acceptance-criteria mapping

- Player never attempts provider audio URL: covered by coordinator guard test.
- One bad file does not crash the player: covered by engine-failure state test.
- Player state unit tests: introduced in this slice.
