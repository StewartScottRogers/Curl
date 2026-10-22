---
id: BL-3097
title: Stop Invoke-TaskSplit pushing a lane's unintegrated commits after a run
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/TaskStash.ps1, DarkFactory/ShiftRunner.ps1]
requirement: none
created: 2026-10-22
completed:
---
# BL-3097 — Stop Invoke-TaskSplit pushing a lane's unintegrated commits after a run

## Goal

After a run stops at its cost cap, the split in a lane commits the board change and pushes nothing. `Invoke-Integrate` pushes every commit, as ADR-0759 says.

## Context

Found by the shift-end review (ADR-0710) of `bc792b5be...2c76ddbde`. Both the code-reviewer and the silent-failure-hunter reported it.

`Invoke-TaskSplit` (`DarkFactory/TaskStash.ps1:162`) always runs `push -q origin HEAD:$Branch` first. `-PullOnRefusal` decides only what happens after a refused push.

The post-run path (`DarkFactory/ShiftRunner.ps1:195`) does not pass `-PullOnRefusal`. On that path:

- A lane's HEAD holds whatever the over-cap run committed, because `$LaneForbidden` forbids pushing but not committing.
- If origin has not moved, the push fast-forwards. That code reaches the shared branch without the rebase, build and fast tests that `Invoke-Integrate` runs under the lock.

Before this change, lanes never pushed there: the old code was `if (-not $Lane) { git push }`. The comment at `TaskStash.ps1:163-165` and ADR-0759 both say the split commit is left for integration.

The push on the failure path (`TaskStash.ps1:176`) has the same problem. The existing test, "after a run a refused push is left to integration", covers only a refused push.

## Acceptance criteria

- [ ] On the post-run path in a lane, `Invoke-TaskSplit` pushes neither the split commit nor the block commit. The single runner (`-not $Lane`) still pushes, as it did before.
- [ ] A `-TestTaskStash` case with a push stub that accepts checks that no push is issued on a lane's post-run path. The case fails on the current code.
- [ ] The comment at `TaskStash.ps1:163-165` and ADR-0759 still describe what the code does.
- [ ] `RunDarkFactory.ps1 -TestTaskStash` passes.

## Notes

## Log

- 2026-10-22: Created.
