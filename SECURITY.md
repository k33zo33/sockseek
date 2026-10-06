# Security policy

Sockseek is a local-first desktop music manager, Soulseek downloader and local audio player. Please report security issues privately instead of opening a public issue when the report includes a vulnerability, credential exposure, auth bypass or filesystem traversal risk.

## Supported versions

Public beta is not released yet. Until a tagged beta exists, security review applies to the current `master` branch and any release-candidate artifacts explicitly shared by the maintainer.

## Reporting a vulnerability

Use GitHub private vulnerability reporting if it is enabled for this repository. If it is not available, contact the maintainer privately before posting technical exploit details in public issues.

Include:

- affected commit, tag or release artifact name;
- operating system and install method;
- concise reproduction steps;
- expected impact;
- the smallest redacted diagnostic export or log excerpt needed to reproduce the issue.

Do not include:

- Spotify, YouTube, Bandcamp or MusicBrainz tokens;
- OAuth codes, PKCE code verifiers or client secrets;
- Soulseek usernames/passwords;
- full `Authorization` headers or local session tokens;
- private playlist URLs with sensitive query parameters;
- personal music library paths unless they are required and redacted.

## Local-first security expectations

- The daemon should bind to loopback by default.
- Protected `/api/v1` endpoints require the local session token.
- Provider secrets are stored through `ISecretStore`; SQLite stores opaque secret references only.
- Diagnostics should be produced through the redacted export path documented in `docs/SECURITY.md`.

## Public disclosure

Please allow a reasonable coordination window before public disclosure. If a report affects a published artifact, the fix should identify the affected version, include tests where practical and update release notes or known limitations.
