# Sprint 15 soak memory retention

## Goal

Stop uncontrolled memory growth during the Sprint 15 long-running server soak by pruning completed Core workflow state when the server history cap evicts the corresponding workflow.

## Current-state findings

`EngineStateStore` caps completed server workflow history, but `DownloadEngine` is a long-lived server process and still retains root jobs in `Queue`, registered jobs in `DownloadJobTracker`, job contexts, workflow auto-profile state and completed root tasks. The failed 8 hour soak reached the server workflow cap while managed heap continued to grow.

## In scope

- Add explicit completed-workflow pruning to Core engine state.
- Wire server workflow history eviction to Core pruning.
- Add focused tests for bounded retained Core/server state.
- Update Sprint 15 performance evidence if validation passes.

## Out of scope

- Changing Soulseek behavior, provider authorization, provider playback policy or desktop API contracts.
- Changing persistence schema, migrations or package topology.
- Completing the public beta go/no-go while soak evidence remains incomplete.

## Files and projects affected

- `Sockseek.Core`
- `Sockseek.Server`
- `Sockseek.Core.Tests`
- `Sockseek.Server.Tests`
- Sprint 15 documentation if evidence changes.

## API, schema and event changes

No HTTP/OpenAPI, provider contract or database schema change is planned. Core may gain internal/public engine retention methods used by the server adapter; the server event surface remains unchanged.

## Implementation sequence

1. Add a pruning API to Core engine internals that removes inactive workflow jobs from queue, tracker, context and per-workflow caches.
2. Ensure `DownloadEngine.RunAsync` does not retain completed root task references indefinitely.
3. Raise a server-local notification when `EngineStateStore` prunes completed workflow history.
4. Have `EngineSupervisor` prune the matching Core workflow from the current engine.
5. Add tests proving pruned workflows disappear from both server history and Core retained state.

## Testing strategy

- Targeted Core tests for tracker/context/queue pruning.
- Targeted Server tests for workflow history eviction invoking Core pruning.
- Short opt-in soak smoke after build.
- Full Release build and relevant test suite before committing.

## Migration and rollback

No migration. Rollback is reverting the retention hook and tests; historical in-memory state would again be retained until engine shutdown.

## Security, privacy and license impact

No new secrets, network access, external provider audio or license change. Pruning removes stale in-memory job metadata after the existing server history cap evicts it.

## Risks and stop conditions

Stop if pruning would remove active workflows, break retry/cancel semantics for still-visible workflows or require changing public API contracts. Pruned workflows are already absent from server history, so command behavior for visible jobs must remain unchanged.

## Acceptance-criteria mapping

Supports Sprint 15's "Nema nekontroliranog memory growtha u osmosatnom runu" criterion by aligning Core retention with server workflow history retention.
