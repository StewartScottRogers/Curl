# ADR-0756 — The CI watch defers a CI task a green run proves fixed, and never files one with empty touches

- Status: Accepted
- Date: 2026-10-22
- Task: BL-3028
- Decided by Claude under Stewart's delegation.

## Context

On 2026-10-21 the coordinator's CI watch (BL-987) filed BL-3026 for a failed Quality gates job
step with `touches: []`, because touches came only from a test project and a job failure names
none. The pre-claim check (BL-3001) rightly calls such a task unready, so no lane took it. The
failure was already fixed and CI was green on 1afd80a99, but nothing retired the task: the watch
only declined to file a covered failure.

## Decision

- A job failure's touches are the files its `::error file=` annotations name and the source files
  that declare each member "outside a gate" (`Type.Method` -> `*/Type.cs` and its partials), as
  far as they exist in the repository. When a failure names no file or project, the task falls
  back to `Measure-CodeQuality.ps1` for a quality, coverage, complexity or CRAP step, the script
  that step runs, and to `Curl.slnx` for any other step, which the build and test steps build.
  The fallback is never `.github/workflows/ci.yml`: it is a guard file, and a lane can take no
  task that touches it. No non-audit CI task is filed with empty touches.
- When the newest finished CI run on the branch is green and its commit contains the filing of
  a Backlog task titled `Fix CI failure ...`, `Fix CI build ...` or `Fix flaky CI test ...`, the
  watch moves that task to **Deferred**, not Done, with "already fixed, CI green on <sha>",
  through `task-board.ps1 move` in the ci-watch worktree, pushed like a filing. Done is kept for
  work a lane did and verified, and the board moves only a task in Doing to Done; Deferred keeps
  the task readable and lets an interactive session reopen it. A task in Doing is left to its
  lane. Only a fully green run counts: a red run, even one that does not name the task's
  failure, may have skipped the job or the tests that would.

## Consequences

A CI failure in a job step is claimable by a lane at once, and a CI task fixed by other work
leaves the queue at the next green run instead of holding a High slot. A task the watch defers
wrongly costs one interactive move back to Backlog.
