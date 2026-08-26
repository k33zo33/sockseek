# PLANS.md

Create an execution plan before implementation when work:

- changes more than three projects;
- introduces or changes a database migration;
- changes public API contracts;
- changes process startup, authentication or packaging;
- touches the existing `DownloadEngine` orchestration;
- changes provider authorization or synchronization behavior;
- spans more than one pull request.

Save plans under `docs/plans/<sprint>-<task>.md`.

## Required plan structure

```md
# <Sprint and task>

## Goal

## Current-state findings

## In scope

## Out of scope

## Files and projects affected

## API, schema and event changes

## Implementation sequence

## Testing strategy

## Migration and rollback

## Security, privacy and license impact

## Risks and stop conditions

## Acceptance-criteria mapping
```

## Plan rules

- Inspect real code before asserting current behavior.
- Prefer small vertical changes over a broad rewrite.
- Identify backward-compatibility impact explicitly.
- A plan may reference future work but must not implement future sprint scope.
- Update the plan when implementation discoveries materially change it.
- Stop and request an ADR when a locked product decision would need to change.
