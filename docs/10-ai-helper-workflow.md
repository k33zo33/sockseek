# AI helper workflow

This repository includes a small wrapper for read-only helper/reviewer passes:

```bash
./tool/ai_helper.sh review
./tool/ai_helper.sh ask "<question>"
```

## Helpers

- default helper: `gemini`
- optional helper: `claude`

Override per call:

```bash
./tool/ai_helper.sh review --helper gemini
./tool/ai_helper.sh review --helper claude
./tool/ai_helper.sh ask --helper gemini "What is the safest way to refactor this?"
```

Environment overrides:

```bash
export HELPER_DEFAULT=gemini
export GEMINI_MODEL=<optional-model>
export CLAUDE_MODEL=<optional-model>
```

## Modes

### `review`

Read-only diff review.

Priority order:
1. staged diff
2. working tree diff
3. last commit (`HEAD~1..HEAD`)

Optional explicit range:

```bash
./tool/ai_helper.sh review --ref origin/master..HEAD
```

### `ask`

One-shot repository question for architecture, debugging, or implementation guidance.

```bash
./tool/ai_helper.sh ask "Summarize the risks in the current Dockerfile"
```

## Constraints

- Helpers are used in **read-only** mode.
- One request in, one answer out.
- If a helper asks a follow-up question, stop and route that question to the human instead of entering a loop.
- Prefer using the helper for code review, architecture review, and unblockers—not as the primary coding engine.

## Authentication and environment notes

Helper availability is environment-dependent.

Known Sprint 0 baseline behavior in this workspace:

- `gemini` CLI may fail with an unsupported or ineligible client-tier error
- `claude` CLI may fail when local OAuth authentication has expired

Treat these as workflow/environment issues, not as repository correctness failures.

Practical guidance:

1. run the local validation first
2. attempt helper review if the environment looks healthy
3. if helper auth fails, capture the exact error in the baseline report or task notes
4. continue with local review instead of blocking the repository change on helper availability alone

## Practical use in this repo

Recommended cadence:

1. inspect code and docs locally first
2. make the smallest coherent change
3. run local validation
4. run `./tool/ai_helper.sh review`
5. address review findings that materially improve correctness/safety/scope
6. commit

This keeps helper usage cheap, deterministic, and easy to audit.
