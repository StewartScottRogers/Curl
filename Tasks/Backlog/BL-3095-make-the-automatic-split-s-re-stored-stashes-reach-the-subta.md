---
id: BL-3095
title: Make the automatic split's re-stored stashes reach the subtasks
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/TaskStash.ps1, DarkFactory/Prompts.ps1]
requirement: none
created: 2026-10-22
completed:
---
# BL-3095 — Make the automatic split's re-stored stashes reach the subtasks

## Goal

When a split re-stores an over-cap task's stash under the new task IDs, `Restore-TaskStash` finds that stash and applies it whenever a subtask that continues the work is claimed.

## Context

Found by the shift-end review (ADR-0710) of `bc792b5be...2c76ddbde`. The code-reviewer reported it, and the behaviour was confirmed in a scratch git repository.

Step 3 of `$SplitPrompt` (`DarkFactory/Prompts.ps1:428-430`) tells the split run to run `git stash store -m "darkfactory <new id> {STAMP}" {SHA}`.

- `git stash store -m` writes the message as the bare reflog subject, with no `On <branch>: ` prefix. `git stash list --format='%H %gs'` then shows `<sha> darkfactory BL-2 ...`.
- `Get-TaskStashSha` (`DarkFactory/TaskStash.ps1:70-77`) matches `^(\S+) .*: darkfactory <id> `, which needs a `: ` before `darkfactory`. It therefore never finds a re-stored stash.
- The result is that subtasks start from scratch, which defeats the carry-over ADR-0759 promises.

Two more facts about git, both seen in the scratch repository, make this worse:

- Storing a hash that is already the top of `refs/stash` writes no entry at all, and still exits 0.
- Storing the same hash a second time writes no second entry.

So at most one subtask gets an entry, and often none does.

A failed split has a related problem. It puts the board back as it was (`TaskStash.ps1:171-178`), but leaves behind any stash it already stored under a new ID. A later `task-board.ps1 new` can reuse that freed ID, and the task that gets it would then have another task's work applied.

## Acceptance criteria

- [ ] `Get-TaskStashSha` finds a stash written with `git stash store -m "darkfactory <id> <stamp>" <sha>`, as well as one written by `Save-StrayChanges` with `stash push`.
- [ ] When one stash is meant for several subtasks, every subtask that continues the work can find it. One way: the shift stores it itself after the split run, once per new ID. Another: the subtasks record the hash and `Restore-TaskStash` falls back to it.
- [ ] A failed split drops every stash entry it added, and only those.
- [ ] `-TestTaskStash` has real-git cases that store a stash for two new IDs and restore each one, and a case that checks a failed split leaves the stash list as it was. These cases fail on the current code.
- [ ] `RunDarkFactory.ps1 -TestTaskStash` passes.

## Notes

## Log

- 2026-10-22: Created.
