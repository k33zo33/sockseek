# Beta limitations and release notes

Sockseek is not approved for public beta distribution yet. The completed Sprint 15 decision allows closed/internal beta testing only while Sprint 16 addresses Soulseek compliance.

## Release status

- Public beta: blocked by ADR-0009 until Soulseek compliance is resolved.
- Closed/internal beta: allowed for packaging, performance, diagnostics, provider import and local playback validation.
- Stable release: blocked until public beta gates, code signing/rollback decisions and cross-platform packaging decisions are complete.
- The completed Sprint 15 go/no-go evidence is tracked in `docs/beta-go-no-go.md`; Sprint 16 must not supersede it without an accepted ADR.

## Legal-use notice

Sockseek is a local-first music manager, Soulseek downloader and local audio player. Users are responsible for following applicable law and the terms of any network or service they use. Only share, download, import or play material that you are legally allowed to use.

External services are playlist or metadata sources only. Spotify, YouTube, Bandcamp and MusicBrainz are never audio sources for Sockseek playback or downloading.

## Provider limitations

| Provider | Current capability | Known limitation |
| --- | --- | --- |
| Spotify | OAuth PKCE account connection, playlist listing, playlist item import and sync through metadata APIs. | Spotify development-mode quota and allowlist restrictions may block unregistered users. No Spotify playback, audio streaming, audio URL use or downloading. |
| YouTube | OAuth PKCE account connection, `mine=true` playlist listing, playlist item import and sync through metadata APIs. | Token expiry/revocation requires reconnect. No YouTube playback, iframe player, audio extraction, offline cache or downloading. |
| Bandcamp | Public album/track URL metadata import without credentials. | Parser depends on public page structured metadata; failures are localized to import. No cookies, credentials, playback or downloading from Bandcamp. |
| MusicBrainz | Metadata lookup/enrichment with rate limiting and cache behavior. | Metadata-only; no MusicBrainz account, ListenBrainz history, playback, download or scrobbling support. |
| Soulseek | Search, candidate ranking, album folder discovery, download workflows and local/progressive playback of downloaded files. | Public beta is blocked until compliance coverage for wishlist, upload/sharing, chat and privilege recognition is resolved. |

## Operational limitations

- Windows release-candidate packaging is internally verified with legal artifacts, SBOM and SHA256 manifest.
- The Windows installer is currently script-based PowerShell, not a signed MSI/MSIX.
- Docker is a secondary headless path. The Sprint 15 helper reaches Docker Desktop from the validation session, records Windows Docker Desktop pipe/config diagnostics, preserves the repo working directory inside bounded Docker jobs and passed the full image build, CLI help and daemon `/health` smoke on 2026-10-13 after Docker Desktop WSL storage was moved to `F:\Docker\DockerDesktopWSL`.
- Linux desktop packaging is not complete; the current native media package path is Windows-focused.
- The Sprint 15 eight-hour soak gate passed for commit `93a8c63`. Exact packaged Windows commit `6da97fd` also passed the rendered 100k Library, 10k Search and 10k Playlist virtualization traces; Windows 11 and Ubuntu target-OS public-beta package smoke remain open.

## Diagnostics for closed beta testers

Generate per-build tester instructions with the exact commit, source URL, legal-use notice, limitations and redaction guidance before sharing a closed/internal beta build:

```powershell
.\scripts\write_closed_beta_tester_instructions.ps1 `
  -ArtifactName "Sockseek Windows closed beta" `
  -Version "3.0.5" `
  -OutputPath artifacts\closed-beta-tester-instructions.md
```

- Use the app `Copy diagnostics` action when it is available, or the smallest redacted crash detail or log excerpt needed to reproduce an issue.
- Follow `docs/diagnostics-feedback.md` for crash reports, reproducible diagnostics and routing.
- Do not paste provider tokens, OAuth codes, PKCE code verifiers, client secrets, Soulseek passwords, full Authorization headers or private playlist URLs with sensitive query parameters.
- Report vulnerabilities privately through the process in `SECURITY.md`.
