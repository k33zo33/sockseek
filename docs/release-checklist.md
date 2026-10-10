# Release checklist

Use this checklist before publishing any public binary, installer, package, or hosted daemon build.

## AGPL / source availability

- [ ] Root `LICENSE` is included unchanged in the release artifact or installer payload
- [ ] Root `THIRD-PARTY-NOTICES` (or a more complete release-specific equivalent) is included in the release artifact or linked from the installer/app
- [ ] Root `SECURITY.md` is included in the release artifact or tester package
- [ ] The product exposes the exact source URL for the released build
- [ ] The exact corresponding source is available to users for the released build
- [ ] A source tag matching the public release has been created and pushed
- [ ] About/License UI (or equivalent packaged notice) includes AGPL no-warranty text and source link

## Build provenance

- [ ] Release notes identify significant modifications honestly
- [ ] Generated OpenAPI artifacts (currently `docs/openapi.json`) are committed for the released build
- [ ] Required migrations are committed for the released build
- [ ] The release notes or artifact metadata identify the exact commit/tag being shipped
- [ ] Release artifact includes an SPDX SBOM at `sbom.spdx.json`
- [ ] Release artifact has a SHA256 manifest for internal verification
- [ ] Release artifact or tester package includes `docs/beta-limitations.md` with beta limitations and legal-use notice

## Security / packaging

- [ ] Local daemon binds safely for the release target
- [ ] Secrets are not logged or bundled in release artifacts
- [ ] Packaging scripts used for the release are committed in the repo
- [ ] If Docker/container artifacts are published, they include the same AGPL/source-link/notices expectations as other public binaries

## Public beta gates

- [ ] `scripts/validate_beta_go_no_go.ps1` passed for the exact release candidate state
- [ ] `docs/beta-go-no-go.md` says public beta is GO for the exact commit/tag being shipped
- [ ] Soulseek compliance is resolved by an accepted ADR that allows the planned public distribution
- [ ] Eight-hour soak evidence is captured and recorded for the release candidate
- [ ] Rendered Desktop virtualization traces are captured for library, search and playlist surfaces
- [ ] Target OS package smoke has passed for every artifact being published

## Product scope guardrails

- [ ] `scripts/run_provider_audio_guard.ps1` passed for the exact commit/tag being shipped
- [ ] No provider playback capability is exposed
- [ ] No provider audio downloading capability is exposed
- [ ] UI language still frames Spotify/YouTube/Bandcamp/MusicBrainz as import/metadata sources only
