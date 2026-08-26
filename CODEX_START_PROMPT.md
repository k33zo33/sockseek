# Initial Codex prompt

Work on repository `k33zo33/sockseek`.

Read in this order:

1. `AGENTS.md`
2. `docs/project-state.yaml`
3. `docs/sprints/sprint-00-baseline-upstream-agpl.md`
4. only the additional documents referenced by that sprint

Implement only Sprint 0.

Before changing files:

- inspect the current branch, working tree, remotes and baseline commit;
- compare the fork against upstream;
- run restore, Release build and the existing test suite;
- create an execution plan under `docs/plans/` if required by `PLANS.md`;
- do not force-push or rewrite `master`.

Locked decisions:

- external services provide playlists and metadata only;
- there is no Spotify, YouTube, Bandcamp or MusicBrainz playback/download;
- all playback comes from local library files or Soulseek downloads;
- the project remains GNU AGPL-3.0;
- the existing Sockseek engine is retained behind an adapter;
- legacy CLI and API behavior must remain functional.

At completion:

- run every validation command required by the sprint;
- review the full diff;
- update the sprint status and `docs/project-state.yaml` only if acceptance criteria pass;
- report changed files, tests, risks, migration impact and next recommended action;
- prepare a PR description with a rollback plan.
