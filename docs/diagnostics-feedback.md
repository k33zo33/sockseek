# Diagnostics and crash feedback

Sprint 15 closed beta feedback must be reproducible without collecting secrets or implying public beta readiness.

## What testers should include

- Exact commit, tag or artifact name.
- Operating system and install method.
- Whether the issue happened in a packaged build, local build or Docker/headless path.
- Provider involved, if any: Spotify, YouTube, Bandcamp, MusicBrainz, Soulseek or none.
- Steps that reproduce the issue from a clean app start.
- Expected behavior and actual behavior.
- The smallest redacted diagnostics text, crash details or log excerpt needed to reproduce the issue.

## Desktop diagnostics

When the Desktop backend banner shows a recoverable problem state, use the `Copy diagnostics` action and paste only the copied text into the issue.

The current diagnostics surface is a safe shell/backend snapshot. It is not a full log export and it must not be replaced with raw logs unless the maintainer asks for a specific redacted excerpt.

## Crash reports

Use the GitHub crash report template for:

- application exits, unhandled exceptions or startup crashes;
- daemon startup failures that close the app or prevent connection recovery;
- native player crashes;
- packaged build crashes that do not reproduce in a local developer run.

Include the Windows Event Viewer application error, terminal exception text or crash dialog details only after removing secrets and private paths.

## Do not include

- access tokens, refresh tokens, OAuth codes, PKCE code verifiers or client secrets;
- Soulseek usernames or passwords;
- full `Authorization` headers or local session tokens;
- private playlist URLs with sensitive query parameters;
- unredacted local library paths, account names or screenshots that expose private data;
- full raw logs when a shorter redacted excerpt is enough.

## Routing

- Public bug reports: use `.github/ISSUE_TEMPLATE/bug_report.md`.
- Crashes: use `.github/ISSUE_TEMPLATE/crash_report.md`.
- Vulnerabilities, auth bypasses, traversal issues or credential exposure: use private vulnerability reporting from `SECURITY.md`.

Public beta remains blocked until `docs/beta-go-no-go.md` says the exact release candidate is GO.
