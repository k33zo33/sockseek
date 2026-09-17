# Sprint 5 search query foundation

## Goal

Provide a tested Desktop-side entry point for typed track and album search requests.

## Current-state findings

- `Sockseek.Api.SockseekApiClient` already exposes typed track and album search endpoints.
- The Desktop Search page is still a placeholder and has no query state or submission model.
- Search requests must remain DTO-based; Desktop must not reference `Sockseek.Core` job objects.

## In scope

- Add track/album mode state.
- Validate the minimum query fields before sending a request.
- Submit typed search requests through the existing API client.
- Expose the returned job summary and a user-visible request error.
- Refresh typed result snapshots and expose candidates, revision and completion state.
- Submit an explicit file-candidate download action using the stable candidate reference.
- Delegate cancel and next-candidate actions to the existing daemon job endpoints.

## Out of scope

- Album-folder download actions and full candidate rendering.
- Progress subscriptions and workflow tree rendering.
- Avalonia layout changes.

## Files and projects affected

- `Sockseek.Desktop/DesktopSearchMode.cs`
- `Sockseek.Desktop/DesktopSearchViewModel.cs`
- `Sockseek.Desktop.Tests/DesktopSearchViewModelTests.cs`

## API, schema and event changes

- No API contract changes.
- No schema, migration or SignalR changes.
- Uses existing typed search endpoints.

## Implementation sequence

1. Add typed search mode and query state.
2. Validate and submit track/album requests.
3. Add result snapshot refresh and candidate state.
4. Add explicit file-candidate download action.
5. Add cancel and next-candidate job actions.
6. Add HTTP contract tests for payload, validation, result and job actions.

## Testing strategy

- Use an in-memory `HttpMessageHandler` to verify endpoint selection and serialized request content.
- Run the focused tests, then the complete `Sockseek.Desktop.Tests` project.

## Migration and rollback

- No migration.
- Rollback is a normal code revert.

## Security, privacy and license impact

- Uses the existing authenticated localhost API client.
- No provider audio capability or external media source is introduced.
- No secrets are logged or stored.
- License remains AGPL-3.0.

## Risks and stop conditions

- The ViewModel is not wired to Avalonia controls until the result/candidate slice defines the screen state.
- Stop if the existing API client requires a public contract change to support the UI.

## Acceptance-criteria mapping

- Establishes the query-submission foundation for “korisnik može pretražiti” without CLI usage.
- Keeps the request path non-blocking and cancellation-aware.
- Establishes the explicit single-file download request path for the first vertical flow.
- Establishes the cancel and next-candidate paths required for user-controlled recovery.