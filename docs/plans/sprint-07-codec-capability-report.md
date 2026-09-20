# Sprint 7 - Codec capability report

## Goal

Expose local player codec capability information through the existing versioned system capabilities endpoint.

## Current-state findings

- `/api/v1/system/capabilities` already returns `SystemCapabilitiesDto`.
- Sprint 7 requires a codec capability report and later fixture validation for MP3, FLAC, Ogg, Opus, WAV and M4A.
- LibVLCSharp is now the selected and registered local player engine.

## In scope

- Extend `SystemCapabilitiesDto` with local player capability metadata.
- Include the Sprint 7 codec list with conservative fixture-validation status.
- Update source-generated JSON metadata, OpenAPI output and server tests.

## Out of scope

- Real codec fixture playback tests.
- Progressive playback enablement.
- Desktop rendering of the report.
- Any provider playback/download capability.

## Files and projects affected

- `Sockseek.Api`
- `Sockseek.Server`
- `Sockseek.Server.Tests`
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
- OpenAPI contract tests verify schema generation.
- Full Release build and test suite before commit.

## Migration and rollback

- No migration.
- Rollback removes the new DTO fields and capability population.

## Security, privacy and license impact

- Report is metadata only and exposes no local file paths or secrets.
- No provider audio URL, playback provider, stream or download method is introduced.

## Risks and stop conditions

- Stop if the report would need to claim unverified codec support as fully tested.
- Stop if progressive playback would need to be enabled before Sprint 8 fixture evidence exists.

## Acceptance-criteria mapping

- Partially satisfies Sprint 7 codec capability report.
- Does not satisfy the required codec fixture integration tests yet; report marks those codecs as requiring fixture validation.
