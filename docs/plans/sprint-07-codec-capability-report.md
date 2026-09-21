# Sprint 7 - Codec capability report

## Goal

Expose local player codec capability information through the existing versioned system capabilities endpoint.

## Current-state findings

- `/api/v1/system/capabilities` already returns `SystemCapabilitiesDto`.
- Sprint 7 requires a codec capability report and fixture validation for MP3, FLAC, Ogg, Opus, WAV and M4A.
- LibVLCSharp is now the selected and registered local player engine.

## In scope

- Extend `SystemCapabilitiesDto` with local player capability metadata.
- Include the Sprint 7 codec list with local-file fixture validation status.
- Update source-generated JSON metadata, OpenAPI output and server tests.

## Out of scope

- Progressive playback codec fixture tests.
- Progressive playback enablement.
- Desktop rendering of the report.
- Any provider playback/download capability.

## Files and projects affected

- `Sockseek.Api`
- `Sockseek.Server`
- `Sockseek.Server.Tests`
- `Sockseek.Player.Tests`
- `docs/player-codec-fixture-matrix.md`
- `docs/openapi.json`
- `docs/plans`

## API, schema and event changes

- Adds a `player` object to `SystemCapabilitiesDto`.
- No database schema or SignalR event changes.

## Implementation sequence

1. Add player and codec capability DTOs.
2. Populate capabilities in `EngineSupervisor`.
3. Add/update server endpoint tests.
4. Build to regenerate OpenAPI.
5. Run targeted and full validation.

## Testing strategy

- Server system endpoint tests verify player capability payload.
- Player LibVLC fixture tests verify local-file startup for MP3, FLAC, Ogg, Opus, WAV and M4A.
- OpenAPI contract tests verify schema generation.
- Full Release build and test suite before commit.

## Migration and rollback

- No migration.
- Rollback removes the new DTO fields and capability population.

## Security, privacy and license impact

- Report is metadata only and exposes no local file paths or secrets.
- No provider audio URL, playback provider, stream or download method is introduced.

## Risks and stop conditions

- Stop if the report would need to claim progressive codec support before Sprint 8 fixture evidence exists.
- Stop if progressive playback would need to be enabled before Sprint 8 fixture evidence exists.

## Acceptance-criteria mapping

- Satisfies Sprint 7 local-file codec capability report.
- Local-file codec fixture tests now cover startup playback for MP3, FLAC, Ogg, Opus, WAV and M4A; progressive playback remains Sprint 8 scope.
