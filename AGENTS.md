# AGENTS.md

## Project goal

Extend the existing `k33zo33/sockseek` fork into a local-first desktop music manager, Soulseek downloader and local audio player.

External services such as Spotify, YouTube, Bandcamp and MusicBrainz are playlist or metadata sources only. They are never audio sources for the internal player.

## Mandatory product decisions

- Continue from the existing Sockseek fork; do not rewrite the Soulseek engine from scratch.
- The complete product remains licensed under GNU AGPL-3.0.
- All playback uses a local library file, a completed Soulseek download, or a supported progressive Soulseek download.
- Do not implement Spotify, YouTube, Bandcamp or MusicBrainz playback or audio downloading.
- Do not introduce `IPlaybackProvider`, provider audio URLs, `GetAudioStreamAsync`, or `DownloadTrackAsync` into provider contracts.
- The application is local-first. Music, database, Soulseek credentials and provider tokens remain on the user's machine.
- The desktop UI uses Avalonia and communicates with a separate local ASP.NET Core daemon through localhost HTTP and SignalR.
- Keep the existing CLI and legacy daemon API working unless an accepted ADR explicitly deprecates them.
- UI and new application layers must not reference mutable `Sockseek.Core` job objects directly.
- Any change to framework, license, database, process topology, provider scope or audio-source policy requires a new ADR.

## Read before implementation

Always read:

- `docs/project-state.yaml`
- the active file under `docs/sprints/`

Then read only the documents listed under **Required context** in that sprint.

Use `docs/SPECIFICATION.md` only for unresolved details; do not load it by default for every task.

## Repository boundaries

- `Sockseek.Domain`: BCL only; no EF Core, Avalonia, provider SDKs, filesystem or `Sockseek.Core`.
- `Sockseek.Application`: may depend on Domain and integration abstractions; no Avalonia, provider SDK or concrete persistence.
- `Sockseek.Infrastructure`: persistence, filesystem and OS integration; no Desktop dependency.
- `Sockseek.Integrations.*`: provider-specific adapters; no Desktop or `Sockseek.Core` internals.
- `Sockseek.Player`: local media only; no external streaming provider SDK.
- `Sockseek.Server`: application host, persistence, player and the single adapter boundary to existing Core.
- `Sockseek.Desktop`: API contracts/client and Avalonia only; no Core, EF DbContext or provider SDK.

## Required workflow

1. Inspect the current repository and active sprint.
2. Confirm the baseline, branch and working tree state.
3. Create or update an execution plan when required by `PLANS.md`.
4. Implement only the active sprint scope.
5. Add or update automated tests.
6. Run the validation commands required by the sprint.
7. Review the complete diff for scope creep, security and backward compatibility.
8. Update sprint status and `docs/project-state.yaml` only after acceptance criteria pass.
9. Report changed files, tests, risks, migrations and incomplete acceptance criteria.

## Build and validation

```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
```

Run targeted project tests in addition to the full suite when the active sprint names them.

## Security rules

- Never log access tokens, refresh tokens, OAuth codes, code verifiers, client secrets, Soulseek passwords or full Authorization headers.
- Store secrets through `ISecretStore`; SQLite stores only opaque secret references.
- Bind the desktop daemon to loopback by default.
- Require the local session token for protected `/api/v1` endpoints.
- Sanitize and canonicalize every remote filename before filesystem use.
- Never delete a physical audio file merely because an external playlist item disappeared.

## Definition of Done

A task is complete only when:

- active sprint acceptance criteria are met;
- relevant existing and new tests pass;
- no undocumented warnings or flaky tests were introduced;
- OpenAPI is updated when contracts change;
- EF migrations and upgrade tests exist when persistence changes;
- no provider audio capability was introduced;
- documentation and ADRs are updated in the same change;
- legacy CLI/API behavior is preserved or an accepted breaking-change decision exists.
