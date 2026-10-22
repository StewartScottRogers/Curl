---
id: BL-3102
title: Make Invoke-TaskSplit's git failure path reset only its own commit and abort a failed rebase
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/TaskStash.ps1]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3102 — Make Invoke-TaskSplit's git failure path reset only its own commit and abort a failed rebase

## Goal

When a split's push is refused and then the pull or the commit fails, `Invoke-TaskSplit` leaves the lane worktree clean and out of any rebase, and removes only the commit it made, if it made one.

## Context

Found by the shift-end review (ADR-0710) of `bc792b5be...2c76ddbde`. Both the silent-failure-hunter and the code-reviewer reported it.

In `Invoke-TaskSplit` (`DarkFactory/TaskStash.ps1:161-169`), the result of the split commit is discarded with `| Out-Null`.

On the `-PullOnRefusal` path, which the claim takes (`Claim.ps1:284`), a refused push is followed by `pull --rebase`. That pull can stop on a conflict. This is likely, because step 5 of the split prompt edits other tasks' `depends-on` lines, and other lanes edit those same files. When it stops:

1. `Invoke-Git` returns `$false`.
2. The code runs `reset --hard HEAD~1` while the rebase is still in progress.
3. At that point HEAD is origin's tip, so the reset throws away origin's last commit in the local checkout.
4. The rebase stays in progress. The checkout, clean, block commit and push that follow all run on that broken state.
5. Every later git command in the lane fails with "rebase in progress", and the lane is stuck.

A failed commit causes the same problem, for example when `index.lock` is held or a hook fails. `HEAD~1` is then the lane's claim commit, which is already pushed, and the reset removes it.

## Acceptance criteria

- [x] `Invoke-TaskSplit` records HEAD before its split commit. The failure path resets to that recorded hash, and only when the commit succeeded. It never resets to `HEAD~1`.
- [x] A failed `pull --rebase` is followed by `git rebase --abort` before the failure path runs.
- [x] `-TestTaskStash` covers a failed commit and a conflicting pull, using a stubbed `-Git`, and checks which git commands are issued, with no reset to `HEAD~1`. These cases fail on the current code.
- [x] `RunDarkFactory.ps1 -TestTaskStash` passes.

## Notes

- Added a `-HeadSha` scriptblock (default `git rev-parse HEAD`) because `-Git` returns only success. A failed commit is now blocked as "the commit of the split failed" (it used to count as split) and nothing is reset.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 0bf8c043 c341d42b 8e6335df 95ad9451.
- 2026-10-22: Doing -> Done. Invoke-TaskSplit resets only to its recorded hash, aborts a failed rebase; tests cover both
