# ADR-0006: Use SQLite with EF Core for application state

## Status
Accepted

## Context
The new product needs durable playlists, canonical track identities, library files, provider sync state and playback queue state.

## Decision
Use a local SQLite database managed through EF Core migrations. Provider secrets are not stored in readable database columns; the database stores opaque secret references only.

## Consequences
- Every schema change requires a migration and upgrade test.
- Destructive migrations create a backup first.
- Rollback restores the backup; database downgrade is not supported.
- Provider removal never deletes physical music automatically.
