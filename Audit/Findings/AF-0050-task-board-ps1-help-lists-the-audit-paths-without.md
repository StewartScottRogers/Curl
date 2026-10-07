---
id: AF-0050
title: task-board.ps1 help lists the audit paths without the four guard files the code also treats as audit paths
auditor: truthfulness
severity: Low
status: proposed
reason:
key: truthfulness:.claude/skills/task-board/task-board.ps1:Test-AuditPath:false-help
reproduction: none
task: none
tasks:
found: 2026-10-07
found-at: 5a627a2fb4baf7b4b2662dc309939ec576dcad20
scorecard: 2026-10-07_0844.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0050 - task-board.ps1 help lists the audit paths without the four guard files the code also treats as audit paths

## Summary

Low finding from the truthfulness auditor at `.claude/skills/task-board/task-board.ps1:19`: task-board.ps1 help lists the audit paths without the four guard files the code also treats as audit paths.

## Evidence

Location: `.claude/skills/task-board/task-board.ps1:19`

The comment-based help for 'next' (lines 17-22) says a task 'whose touches name an audit path (Audit, Audit/..., .claude/agents/audit-*), is never offered' and that inside a shift 'new' refuses an audit-path task. Test-AuditPath (lines 205-215) also counts .claude/hooks/guard-audit-paths.ps1, .claude/settings.json, .github/workflows/ci.yml and .claude/skills/task-board/task-board.ps1 as audit paths. Parse-Task (line 171) makes such tasks interactive only, and 'new' (lines 498-502) refuses them in a lane. An agent reading the help would expect a task touching ci.yml or settings.json to be lane-eligible and fileable from a lane, and neither is true.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path .claude/skills/task-board/task-board.ps1 -Pattern 'path \(Audit, Audit/\.\.\., \.claude/agents/audit-\*\)','^\s+''\.github/workflows/ci\.yml'''
```

- Expected: The help's list of audit paths includes the guard files that Test-AuditPath names (or the help points at them).
- Actual: task-board.ps1:19: path (Audit, Audit/..., .claude/agents/audit-*), is never offered ; task-board.ps1:209: '.github/workflows/ci.yml', (inside Test-AuditPath, missing from the help)

## Re-audits

## Log

- 2026-10-07: filed proposed.
