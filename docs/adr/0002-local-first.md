# ADR-0002: Use a local-first architecture

## Status
Accepted

## Context
Soulseek connections, local files, provider credentials and playback are user-specific and should not be centralized.

## Decision
Run the application on the user's computer. Store music, SQLite state, Soulseek configuration and provider secrets locally. The daemon binds to loopback by default.

## Consequences
- No central cloud Soulseek downloader.
- One user controls one local engine/account context.
- Desktop packaging must include and supervise the local daemon.
- Remote access is post-MVP and requires a separate authentication ADR.
