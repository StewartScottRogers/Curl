---
id: BL-1888
title: Fix AF-0136: task-board.ps1 help lists the exact audit paths 'next' withholds, but the code also treats any folder that holds them (.claude, .github, .claude/skills) as an audit path
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [.claude]
requirement: none
created: 2026-10-09
completed:
---
# BL-1888 — Fix AF-0136: task-board.ps1 help lists the exact audit paths 'next' withholds, but the code also treats any folder that holds them (.claude, .github, .claude/skills) as an audit path

## Goal

The defect the audit office reported as AF-0136 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0136 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0136-task-board-ps1-help-lists-the-exact-audit-paths-ne.md`.

Location: `.claude/skills/task-board/task-board.ps1:18`

Location: `.claude/skills/task-board/task-board.ps1:18`

Help lines 17-23: a task 'whose touches name an audit path - Audit, Audit/..., .claude/agents/audit-*, Gap, Gap/..., .claude/agents/gap-*, or one of the guard files ... (Test-AuditPath) - is never offered either'. Test-AuditPath (lines 217-221) also returns true for any ancestor folder of a guard file or auditor/analyst agent: '# A folder that holds a guard file or an auditor or analyst agent (".claude", ".github", ".claude/skills" and the like) counts too ... (BL-1876)' and '$holders = @($guardFiles + ''.claude/agents/audit-x'' + ''.claude/agents/gap-x'' | Where-Object { $_.StartsWith("$p/", ...) })'. So a task touching .claude/agents or .github is interactive-only and 'new' refuses it inside a lane, which the help's list does not say.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path .claude/skills/task-board/task-board.ps1 -SimpleMatch 'whose touches name an audit','(BL-1876)'
```

- Expected: The help's audit-path list also names the folders that hold a guard file or agent (or the code refuses only the listed paths).
- Actual: Line 18 (help) lists only the exact paths; line 219 (code comment above $holders) shows ancestor folders such as .claude and .github count too.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-09: Created.
