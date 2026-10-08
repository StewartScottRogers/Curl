# ADR-0436 — Board-only dark factory pushes say [skip ci]

- **Status:** Accepted
- **Date:** 2026-10-08

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

AF-0089 (BL-1767): CI on `work/dark-factory` was red for 169 minutes on 2026-10-07,
126 of them after the fix was pushed. `.github/workflows/ci.yml` puts every push to a
branch in one concurrency group with `cancel-in-progress: true`, and a full CI run takes
longer than the gap between pushes. In those two hours all 115 runs were cancelled.
Board-only commits started most of them. Between 15:55 and 18:05 local time, 58 of the
branch's commits were `chore(tasks): claim ...`, each pushed on its own.

`ci.yml` is a guard file, so it can change only through the `audit` branch. The fix
has to be in `RunDarkFactory.ps1`.

## Decision

1. Every commit `RunDarkFactory.ps1` makes that changes only `Tasks/` and is pushed on
   its own (head of its push) carries `[skip ci]` in its body: claim, requeue in a claim,
   park, return at shift start, and CI-watch filings. GitHub starts no run for such a
   push, so it no longer cancels the run that is testing the code below it.
2. Renumber and archive commits do not carry it, because they can head an
   integration push whose code needs a run. Single-lane block and requeue commits do not
   carry it either, because their plain `git push` can carry other commits.
3. When the shift-end merge finds a `[skip ci]` commit at the head of the branch, it
   starts CI on it with `gh workflow run CI` (the workflow allows `workflow_dispatch`).
   It then waits as before, so a merge still needs CI to have passed for the exact
   commit being merged.

## Consequences

- With pushes that only claim or park a task skipping CI, each code integration's run
  is cancelled only by the next code integration.
- A board-only commit is never tested on its own. It changes Markdown under `Tasks/`
  only, and the next code push or the shift-end dispatch tests it.
