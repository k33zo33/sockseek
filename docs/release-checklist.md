# Release checklist

Use this checklist before publishing any public binary, installer, package, or hosted daemon build.

## AGPL / source availability

- [ ] `LICENSE` is included unchanged in the release artifact or installer payload
- [ ] `THIRD-PARTY-NOTICES` is included in the release artifact or linked from the installer/app
- [ ] The product exposes the exact source URL for the released build
- [ ] The exact corresponding source is available to users for the released build
- [ ] A source tag matching the public release has been created and pushed
- [ ] About/License UI (or equivalent packaged notice) includes AGPL no-warranty text and source link

## Build provenance

- [ ] Release notes identify significant modifications honestly
- [ ] Generated OpenAPI artifacts are committed for the released build
- [ ] Required migrations are committed for the released build

## Security / packaging

- [ ] Local daemon binds safely for the release target
- [ ] Secrets are not logged or bundled in release artifacts
- [ ] Packaging scripts used for the release are committed in the repo

## Product scope guardrails

- [ ] No provider playback capability is exposed
- [ ] No provider audio downloading capability is exposed
- [ ] UI language still frames Spotify/YouTube/Bandcamp/MusicBrainz as import/metadata sources only
