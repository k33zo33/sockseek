# ADR-0001: Continue under GNU AGPL-3.0

## Status
Accepted

## Context
The existing Sockseek codebase is licensed under GNU AGPL-3.0. The product extends that code with a desktop UI, local daemon, player, persistence and provider integrations.

## Decision
The complete derived product remains open source under GNU AGPL-3.0. Existing license and copyright notices remain intact. Public binaries expose the corresponding source for the exact released version.

## Consequences
- Include LICENSE and THIRD-PARTY-NOTICES in distribution.
- Add an About/License screen with source URL and no-warranty notice.
- Tag release source matching every public binary.
- Backend, UI and packaging scripts remain AGPL-compatible.
- A closed-source product requires a separate legal and licensing decision.
