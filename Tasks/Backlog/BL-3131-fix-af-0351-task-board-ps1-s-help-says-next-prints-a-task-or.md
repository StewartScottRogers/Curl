---
id: BL-3131
title: Fix AF-0351: task-board.ps1's help says next prints a task or 'No task is ready.'; it also prints 'No task can start yet: ...'
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [.claude]
requirement: none
created: 2026-10-22
completed:
---
# BL-3131 — Fix AF-0351: task-board.ps1's help says next prints a task or 'No task is ready.'; it also prints 'No task can start yet: ...'

## Goal

The defect the audit office reported as AF-0351 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0351 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0351-task-board-ps1-s-help-says-next-prints-a-task-or-n.md`.

Location: `.claude/skills/task-board/task-board.ps1:15`

Location: `.claude/skills/task-board/task-board.ps1:15`

The help (line 15) says: 'next     The task /task-run should take next, or "No task is ready.".' The 'next' command (lines 604-616) prints a third answer when ready tasks exist but every one overlaps a Doing task or waits behind one: 'No task can start yet: every ready task overlaps one in Doing or waits behind one that does, e.g. <id> with <id>...' (line 614). A caller that relies on the help would read that line as a task.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path .claude/skills/task-board/task-board.ps1 -Pattern 'or "No task is ready\."|No task can start yet' | ForEach-Object { '{0}: {1}' -f $_.LineNumber, $_.Line.Trim().Substring(0, [Math]::Min(90, $_.Line.Trim().Length)) }
```

- Expected: The help names every answer next gives, including 'No task can start yet: ...'.
- Actual: 15: next     The task /task-run should take next, or "No task is ready.". A ready / 614: Write-Output ('No task can start yet: every ready task overlaps one in Doing or waits behi

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
