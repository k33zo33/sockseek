# Sprint 12 Bandcamp public URL and MetaBrainz metadata

## Goal

Add Bandcamp public URL import and MusicBrainz metadata enrichment as local-first metadata flows, without introducing Bandcamp credentials/cookies, MusicBrainz account features, provider audio playback, provider audio URLs or provider download behavior.

## Current-state findings

- Sprint 9 provider foundation already exposes Bandcamp as `ImportPublicUrl` only and MusicBrainz as `LookupMetadata` only; neither should show account connection.
- Sprint 10 and 11 added reusable provider snapshot persistence, local playlist import and provider API patterns.
- Existing legacy Core extractors can parse some Bandcamp and MusicBrainz URLs for Soulseek job creation, but they live in `Sockseek.Core` and are not the new provider/metadata adapter boundary.
- `ExternalTrackSnapshot` and `ExternalPlaylistItemSnapshot` already carry ISRC and MusicBrainz Recording MBID fields.
- `CanonicalTrackStore` stores ISRC and MusicBrainz Recording MBID and can add provider `TrackSource` rows.
- `TrackIdentityService` already auto-matches by ISRC and MusicBrainz Recording MBID.
- `PlaylistLocalMatchResolver` currently builds `TrackIdentityQuery` without `Isrc` or `MusicBrainzRecordingId`, so imported/enriched playlist metadata is not yet used during local resolution.
- No dedicated `Sockseek.Integrations.Bandcamp` or `Sockseek.Integrations.MusicBrainz` project exists.

## In scope

- Add a Bandcamp public URL importer behind the existing provider abstraction or a narrowly scoped public-URL import service.
- Validate Bandcamp URLs and accept public album/track URLs only; do not support account login, cookies, wishlist scraping, authenticated collections or provider audio downloads.
- Parse Bandcamp fixture HTML/embedded JSON into local playlist snapshots with stable item IDs, track titles, artist, album, duration when available, artwork and original URL.
- Keep Bandcamp parser failures localized to the import operation.
- Add a MusicBrainz metadata client with:
  - meaningful User-Agent,
  - global one-request-per-second limiter,
  - fixture-backed cache,
  - 503 retry/backoff behavior,
  - recording lookup by ISRC,
  - conservative fuzzy recording search for enrichment only.
- Add an enrichment path that stores MusicBrainz Recording MBID and ISRC onto canonical tracks or playlist snapshots without treating low-confidence fuzzy matches as automatic local-file matches.
- Update playlist local matching so imported/enriched ISRC and MBID values are passed into `TrackIdentityService`.
- Add tests proving MusicBrainz capability/UI does not expose account connection.
- Add or update an ADR for ListenBrainz post-MVP playlist/history inclusion decision.

## Out of scope

- Bandcamp login, cookies, private collections, fan account connect or authenticated scraping.
- Bandcamp audio playback, audio streaming, provider download, media URLs or caching provider audio.
- MusicBrainz user playlist import; MusicBrainz is metadata lookup/enrichment only.
- ListenBrainz production implementation unless the ADR explicitly scopes a future prototype.
- Broad playlist resolution UX beyond using enriched metadata in existing matching flows.

## Files and projects affected

- `Sockseek.Integrations.Bandcamp`: likely new public URL parser/import adapter and fixture tests.
- `Sockseek.Integrations.MusicBrainz` or `Sockseek.Integrations.MetaBrainz`: likely new metadata lookup client, limiter, cache and tests.
- `Sockseek.Application` / `Sockseek.Infrastructure`: enrichment service contracts, persistence integration and playlist local match query fixes.
- `Sockseek.Domain`: only if a small metadata result/value object is needed; no provider SDK or persistence dependency.
- `Sockseek.Server`: API wiring only if Bandcamp public URL import or metadata enrichment needs a new endpoint beyond existing provider surfaces.
- `Sockseek.Desktop.Tests`: capability/UI assertions for Bandcamp/MusicBrainz account actions if not already covered.
- `docs/adr`: ListenBrainz post-MVP decision.
- `docs/openapi.json`: update only if endpoint contracts change.

## API, schema and event changes

- Prefer reusing existing provider/public URL import contracts if they are sufficient; otherwise add narrow `/api/v1` endpoints and update OpenAPI in the same commit.
- No EF migration is expected for basic Bandcamp playlist snapshots because item metadata fits `SnapshotJson`.
- MusicBrainz enrichment may need no schema change because `CanonicalTracks.MusicBrainzRecordingId`, `CanonicalTracks.Isrc` and `TrackSources` already exist.
- If a durable MusicBrainz lookup cache requires persistence beyond filesystem/in-memory cache, plan and test the EF migration before implementing.
- No player or download API changes are allowed.

## Implementation sequence

1. Add this execution plan and inspect existing provider/API seams.
2. Fix `PlaylistLocalMatchResolver` to pass snapshot ISRC and MusicBrainz Recording MBID into `TrackIdentityQuery`; add regression tests.
3. Add Bandcamp integration scaffold and fixture parser for public album/track pages.
4. Map Bandcamp public URL imports to local playlist snapshots using existing `ExternalPlaylistSnapshotRecordFactory` and `ExternalPlaylistSnapshotStore`.
5. Add Bandcamp server/API fixture tests if a public URL import endpoint is needed.
6. Add MusicBrainz metadata client with User-Agent, limiter, cache and 503 retry/backoff tests.
7. Add ISRC lookup and conservative fuzzy recording search result mapping.
8. Add enrichment persistence tests proving MBID/ISRC are stored and then used by `TrackIdentityService`.
9. Add MusicBrainz capability/UI no-connect tests and provider-audio forbidden scans.
10. Add ListenBrainz ADR for post-MVP inclusion decision.
11. Run full validation and update Sprint 12 completion docs only after acceptance criteria pass.

## Testing strategy

- Bandcamp:
  - public album fixture parser tests,
  - public track fixture parser tests,
  - malformed/unsupported/private URL rejection tests,
  - parser failure localized to import tests,
  - no credentials/cookies tests.
- MusicBrainz:
  - limiter tests proving no more than one request per second in the tested scheduler,
  - cache hit/miss tests,
  - 503 retry/backoff tests,
  - ISRC lookup tests,
  - fuzzy search confidence tests that never auto-match below accepted thresholds.
- Matching/enrichment:
  - playlist local match tests for ISRC and MBID flowing from playlist item snapshots into `TrackIdentityService`,
  - canonical track persistence tests for MBID/ISRC and `TrackSource` rows.
- Policy:
  - MusicBrainz capability does not show Connect account,
  - Bandcamp does not store credentials/cookies,
  - forbidden provider-audio symbol scan.
- Required validation:
  - `dotnet restore`
  - `dotnet build -c Release`
  - `dotnet test -c Release --no-build`
  - `git diff --check`
  - forbidden provider-audio symbol scan.

## Migration and rollback

- Start with no migration by using existing playlist snapshot and canonical track metadata fields.
- If persistent cache schema becomes necessary, add an EF migration, model snapshot update and upgrade tests in the same chunk.
- Rollback of Bandcamp import should remove registration/API wiring while leaving already imported local playlists intact.
- Rollback of MusicBrainz enrichment should not remove local files, playlist items or canonical tracks.

## Security, privacy and license impact

- Do not store Bandcamp credentials, cookies or authenticated session data.
- Do not log private URLs, cookies, provider tokens or provider response bodies that may contain private data.
- MusicBrainz requests must use a clear User-Agent and respect rate limiting.
- Bandcamp and MusicBrainz remain metadata sources only.
- No provider playback, audio URL, audio extraction or provider download capability is allowed.
- AGPL-3.0 posture is unchanged.

## Risks and stop conditions

- Stop and request a decision if reliable Bandcamp parsing requires login, cookies, DRM bypass, audio extraction or provider download behavior.
- Stop before adding ListenBrainz production support unless the ADR explicitly accepts scope.
- Stop before introducing automatic low-confidence MusicBrainz fuzzy matches as local-file matches.
- Treat any provider audio field/method addition as a release blocker.
- Treat persistent cache schema changes as migration work requiring tests.

## Acceptance-criteria mapping

- Public Bandcamp album URL becomes a local playlist: covered by Bandcamp fixture parser/import and server/API tests.
- Parser change does not break other providers and errors are localized: covered by parser failure tests and existing Spotify/YouTube provider tests.
- No Bandcamp credentials/cookies stored: covered by adapter and policy tests.
- MusicBrainz never exceeds limiter in tested scheduler: covered by limiter tests.
- MBID/ISRC are stored and used in `TrackIdentityService`: covered by enrichment persistence tests and `PlaylistLocalMatchResolver` regression tests.
