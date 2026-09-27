# ADR-0008: Defer ListenBrainz playlist and history integration

## Status
Accepted

## Context
Sprint 12 adds Bandcamp public URL import and MusicBrainz metadata enrichment. MusicBrainz is a public metadata provider for recording IDs, ISRCs, canonical artist/release data and conservative enrichment. It is not an account playlist provider.

ListenBrainz may become useful later for user-specific listening history, recommendations, exports or playlists. That is a separate account integration surface from MusicBrainz metadata lookup and would introduce authentication, sync state, privacy settings and different user expectations.

## Decision
Defer ListenBrainz production support until a post-MVP sprint. Sprint 12 may document the future option, but it must not implement ListenBrainz account connection, playlist import, playback, download, scrobbling or listening-history sync.

Keep MusicBrainz in the current product as metadata lookup/enrichment only:

- no MusicBrainz account connection for playlist import;
- no ListenBrainz token storage in Sprint 12;
- no MusicBrainz or ListenBrainz audio source;
- no automatic low-confidence metadata match as an authoritative local-file match.

## Consequences
- Sprint 12 stays focused on Bandcamp public URL import and MusicBrainz MBID/ISRC enrichment.
- Future ListenBrainz work requires its own plan and security/privacy review before storing account tokens or listening history.
- UI should continue to show MusicBrainz as metadata lookup, not as a connectable playlist provider.
- Provider capability tests remain the guard for avoiding false account affordances.
