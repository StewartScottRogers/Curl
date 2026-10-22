---
id: AF-0351
title: task-board.ps1's help says next prints a task or 'No task is ready.'; it also prints 'No task can start yet: ...'
auditor: truthfulness
severity: Low
status: proposed
reason:
key: truthfulness:.claude/skills/task-board/task-board.ps1:next:false-help
reproduction: none
task: none
tasks:
found: 2026-10-22
found-at: 608a755586679cf34031a0e5b137f01b9d7fb329
scorecard: 2026-10-22_1120.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0351 - task-board.ps1's help says next prints a task or 'No task is ready.'; it also prints 'No task can start yet: ...'

## Summary

Low finding from the truthfulness auditor at `.claude/skills/task-board/task-board.ps1:15`: task-board.ps1's help says next prints a task or 'No task is ready.'; it also prints 'No task can start yet: ...'.

## Evidence

Location: `.claude/skills/task-board/task-board.ps1:15`

The help (line 15) says: 'next     The task /task-run should take next, or "No task is ready.".' The 'next' command (lines 604-616) prints a third answer when ready tasks exist but every one overlaps a Doing task or waits behind one: 'No task can start yet: every ready task overlaps one in Doing or waits behind one that does, e.g. <id> with <id>...' (line 614). A caller that relies on the help would read that line as a task.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path .claude/skills/task-board/task-board.ps1 -Pattern 'or "No task is ready\."|No task can start yet' | ForEach-Object { '{0}: {1}' -f $_.LineNumber, $_.Line.Trim().Substring(0, [Math]::Min(90, $_.Line.Trim().Length)) }
```

- Expected: The help names every answer next gives, including 'No task can start yet: ...'.
- Actual: 15: next     The task /task-run should take next, or "No task is ready.". A ready / 614: Write-Output ('No task can start yet: every ready task overlaps one in Doing or waits behi

## Re-audits

## Log

- 2026-10-22: filed proposed.
