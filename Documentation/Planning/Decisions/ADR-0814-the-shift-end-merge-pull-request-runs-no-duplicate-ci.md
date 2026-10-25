# ADR-0814: The shift-end merge's pull request runs no duplicate CI or Integration jobs

- Status: Accepted
- Date: 2026-10-25
- Task: BL-3512
- Decided by Claude under Stewart's delegation.
- Keeps ADR-0693 and ADR-0811 as they are.

## Context

A `CI` run on `work/dark-factory` that overlapped a shift-end merge took 18m38s from its first
job's start to its last job's end (run 40206727905, head fb6591edc), though its longest job took
under 10 minutes. Its jobs started from 15:29 to 15:40 UTC (`Build and test (windows-latest)`
11 minutes late), because the shift-end merge of 15:16 UTC had started, at once:

- on its pull request (head `factory/merge-20261025-074620`): `CI` 40206122116 (10 jobs) and
  `Integration tests` 40206122117 (3 jobs of 18 to 25 minutes);
- on `master`'s merge commit d9eeeb628: `CI` 40206127122, `Integration tests` 40206127223,
  `Release` 40206127169 (17 jobs) and `Gource` 40206127126 (a 66-minute render).

That is more jobs than the account's concurrent runner limit, and the Windows runners were the
scarcest. The pull request's two runs are pure duplicates: their head is the exact commit whose
push run on `work/dark-factory` already passed on Windows, Linux and macOS (the merge's own
condition, `Wait-MergeCiRun` in `DarkFactory/ShiftEnd.ps1`); `Invoke-MergeToMaster` merges with
`gh pr merge` straight after `gh pr create` and never reads them; the only check `master`
requires (ruleset "Audit guard required on master") is the audit guard job, which its `if`
already skips on that pull request; and `master`'s push runs test the merge commit itself a
moment later. Nothing else in the repository reads runs on `factory/merge-*`.

Separately, every run on `work/dark-factory` waits 5 to 8.5 minutes before its first job, behind
the run before it, because `cancel-in-progress` is `false` there (ADR-0693). Turning it on was
weighed and rejected:

1. ADR-0693's reason still holds: lanes push more often than a run takes, and cancelling the
   running run left Linux and macOS untested for hours.
2. It would break the shift-end merge. With `-Continuous`, the next shift starts beside the
   merge's CI wait (BL-3142) and its lanes push code to `work/dark-factory`. A newer push would
   then cancel the run on the merge's head; `Wait-MergeCiRun` follows a newer head only while
   every commit since is a `[skip ci]` board commit, so the merge would end "not merged" with no
   passing run for the commit it was waiting on.
3. The CI watch ignores cancelled runs, so a red commit whose run was cancelled would go
   unfiled until a later run failed the same way. ADR-0811's one cancel is safe because it
   happens only while the newest finished run is already red and the waiting run carries the
   fix; general cancelling has no such guard.

The wait is bounded by one run, which BL-3464 and BL-3511 brought to about 9 to 13 minutes.

## Decision

On a `pull_request` run whose head branch starts with `factory/merge-`, `ci.yml`'s `test`,
`mcp-pack`, `quality-shard` and `quality` jobs and `integration.yml`'s `integration` job are
skipped by their `if` (`!startsWith(github.head_ref, 'factory/merge-')`). Nothing is cancelled:
the runs start and end at once with their jobs skipped, which GitHub reports as success.
Every push run, `master`'s included, and every other pull request runs as before.
`work/dark-factory`'s concurrency stays as ADR-0693 set it.

## Consequences

- A shift-end merge starts 13 fewer jobs, three of them 18 to 25 minute Integration jobs, so
  the next `work/dark-factory` run competes only with `master`'s push runs for runners.
- The merge's condition is unchanged: a completed, successful `CI` push run on the exact
  `work/dark-factory` commit. The CI watch reads only `work/dark-factory` runs, so it sees no
  change.
- The pull request no longer shows its own test results; the head commit's `work/dark-factory`
  run and `master`'s run on the merge commit hold them.
- `master`'s push runs still compete with the next `work/dark-factory` run. If that alone
  passes 15 minutes, the next lever is `master`'s `Integration tests` (which duplicate the
  merged head's), not `work/dark-factory`'s concurrency.
- The queue wait on `work/dark-factory` stays, at most one run long.
