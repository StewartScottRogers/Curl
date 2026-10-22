---
id: BL-3107
title: Make Invoke-TaskSplit's failure path undo a staged split and keep commits a successful pull brought in
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/TaskStash.ps1]
requirement: none
created: 2026-10-22
completed:
---
# BL-3107 — Make Invoke-TaskSplit's failure path undo a staged split and keep commits a successful pull brought in

## Goal

When `Invoke-TaskSplit` fails after staging or after a successful pull, the board ends exactly as before the split plus the Blocked move, on top of the commits the pull brought in.

## Context

Found by the shift-end review (ADR-0710) of 1c1f9cb05...ea67815c6: the first case by silent-failure-hunter, the second by code-reviewer.

1. Failed commit (`DarkFactory/TaskStash.ps1:207`, BL-3102): when `git commit` fails after `git add -A Tasks` succeeded, no reset runs, and the failure path's `git checkout -q -- Tasks` (`TaskStash.ps1:225`) restores the worktree from the index, which still holds the split's new task files; `git clean` removes only untracked files. The block commit then commits the half-made split along with the Blocked move, though the comment says a failed split leaves nothing of its own behind.
2. Refused push after a successful pull: `pull --rebase` succeeds, the second push is refused, and `reset -q --hard $headBefore` throws away the commits just pulled as well as the split commit (the old `HEAD~1` kept them). The block commit is then made on a stale base, its push is refused with the error sent to `Out-Null`, and the next sync loses the Blocked move.

## Acceptance criteria

- [ ] On a failed split commit, the failure path unstages `Tasks` (`git reset -q -- Tasks` or `git checkout -q HEAD -- Tasks`) before restoring it, so the block commit holds only the Blocked move.
- [ ] After a successful pull, a refused push removes only the rebased split commit (by its own hash or `HEAD~1` checked against the split commit's subject), keeping the pulled commits; `$headBefore` is used only when no rebase happened.
- [ ] `-TestTaskStash` cases cover both: a commit that fails after add (real git, the block commit's tree has no split task file) and a pull that succeeds followed by a refused push (the reset keeps the pulled commits).
- [ ] `RunDarkFactory.ps1 -TestTaskStash` passes.
## Notes

## Log

- 2026-10-22: Created.
