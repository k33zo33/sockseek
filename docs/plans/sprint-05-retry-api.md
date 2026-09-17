# Sprint 5 retry API

## Goal

Expose an explicit retry action for failed desktop download jobs without overloading cancel or next-candidate semantics.

## Current-state findings

- The daemon exposes cancel and next-candidate actions, but no retry route.
- `SockseekApiClient` has no retry method.
- Existing job summaries already expose terminal failure data and available action metadata.
- The desktop queue already owns per-job actions and correlation-bearing API errors.

## In scope

- Add a typed retry action identifier and HTTP route for a known job id.
- Implement retry through the existing engine orchestration boundary.
- Add API client and Desktop queue command support.
- Add server, API-client and Desktop tests.

## Out of scope

- Changing retry policy for individual low-level transfer attempts.
- Retrying arbitrary workflow trees or already successful jobs.
- New persistence schema or provider behavior.

## Files and projects affected

- `Sockseek.Api/Contracts/ServerProtocol.cs`
- `Sockseek.Api/Client/SockseekApiClient.cs`
- `Sockseek.Server/ServerHost.cs`
- Existing Core orchestration seam and relevant tests
- `Sockseek.Desktop/DesktopDownloadQueueViewModel.cs`
- `Sockseek.Desktop/DesktopShellMainWindow.axaml`
- Related API, Server and Desktop tests

## API, schema and event changes

- Add `POST /api/jobs/{jobId}/retry`.
- Return `202 Accepted` when the engine accepts the retry, otherwise `404 Not Found`.
- No database or SignalR schema changes.

## Implementation sequence

1. Identify the existing safe job requeue seam.
2. Add typed protocol/client route support.
3. Add server and engine behavior tests.
4. Add Desktop command and button.
5. Run focused and full validation.

## Testing strategy

- Verify failed/terminal jobs can be retried and invalid jobs are rejected.
- Verify API client sends the retry route.
- Verify Desktop queue command delegates to the API client.
- Run the complete solution test suite.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- Uses the existing authenticated localhost API boundary.
- No secrets are added or logged.
- No provider audio capability is introduced.
- License remains AGPL-3.0.

## Risks and stop conditions

- Stop if the current engine does not expose a safe requeue operation without mutating completed job history.
- Do not implement retry by silently changing next-candidate behavior.

## Acceptance-criteria mapping

- Supplies the explicit retry action required by Sprint 5.
- Keeps cancel and next-candidate behavior semantically distinct.
