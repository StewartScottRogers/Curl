---
id: BL-1677
title: Fix AF-0050: task-board.ps1 help lists the audit paths without the four guard files the code also treats as audit paths
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [.claude]
requirement: none
created: 2026-10-08
completed:
---
# BL-1677 — Fix AF-0050: task-board.ps1 help lists the audit paths without the four guard files the code also treats as audit paths

## Goal

The defect the audit office reported as AF-0050 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0050 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0050-task-board-ps1-help-lists-the-audit-paths-without.md`.

Location: `.claude/skills/task-board/task-board.ps1:19`

Location: `.claude/skills/task-board/task-board.ps1:19`

The comment-based help for 'next' (lines 17-22) says a task 'whose touches name an audit path (Audit, Audit/..., .claude/agents/audit-*), is never offered' and that inside a shift 'new' refuses an audit-path task. Test-AuditPath (lines 205-215) also counts .claude/hooks/guard-audit-paths.ps1, .claude/settings.json, .github/workflows/ci.yml and .claude/skills/task-board/task-board.ps1 as audit paths. Parse-Task (line 171) makes such tasks interactive only, and 'new' (lines 498-502) refuses them in a lane. An agent reading the help would expect a task touching ci.yml or settings.json to be lane-eligible and fileable from a lane, and neither is true.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path .claude/skills/task-board/task-board.ps1 -Pattern 'path \(Audit, Audit/\.\.\., \.claude/agents/audit-\*\)','^\s+''\.github/workflows/ci\.yml'''
```

- Expected: The help's list of audit paths includes the guard files that Test-AuditPath names (or the help points at them).
- Actual: task-board.ps1:19: path (Audit, Audit/..., .claude/agents/audit-*), is never offered ; task-board.ps1:209: '.github/workflows/ci.yml', (inside Test-AuditPath, missing from the help)

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
