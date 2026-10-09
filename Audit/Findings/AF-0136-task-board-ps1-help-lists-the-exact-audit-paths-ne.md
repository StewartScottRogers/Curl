---
id: AF-0136
title: task-board.ps1 help lists the exact audit paths 'next' withholds, but the code also treats any folder that holds them (.claude, .github, .claude/skills) as an audit path
auditor: truthfulness
severity: Low
status: accepted
reason: 
key: truthfulness:.claude/skills/task-board/task-board.ps1:Test-AuditPath:false-help
reproduction: none
task: BL-1888
tasks: BL-1888
found: 2026-10-09
found-at: 71f3acef7ec0d988d2d6b5d967a7b7300156cf44
scorecard: 2026-10-09_0647.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0136 - task-board.ps1 help lists the exact audit paths 'next' withholds, but the code also treats any folder that holds them (.claude, .github, .claude/skills) as an audit path

## Summary

Low finding from the truthfulness auditor at `.claude/skills/task-board/task-board.ps1:18`: task-board.ps1 help lists the exact audit paths 'next' withholds, but the code also treats any folder that holds them (.claude, .github, .claude/skills) as an audit path. Reappeared; previously AF-0050. Reported by an auditor flagged unreliable in 2026-10-09_0647.md.

## Evidence

Location: `.claude/skills/task-board/task-board.ps1:18`

Help lines 17-23: a task 'whose touches name an audit path - Audit, Audit/..., .claude/agents/audit-*, Gap, Gap/..., .claude/agents/gap-*, or one of the guard files ... (Test-AuditPath) - is never offered either'. Test-AuditPath (lines 217-221) also returns true for any ancestor folder of a guard file or auditor/analyst agent: '# A folder that holds a guard file or an auditor or analyst agent (".claude", ".github", ".claude/skills" and the like) counts too ... (BL-1876)' and '$holders = @($guardFiles + ''.claude/agents/audit-x'' + ''.claude/agents/gap-x'' | Where-Object { $_.StartsWith("$p/", ...) })'. So a task touching .claude/agents or .github is interactive-only and 'new' refuses it inside a lane, which the help's list does not say.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path .claude/skills/task-board/task-board.ps1 -SimpleMatch 'whose touches name an audit','(BL-1876)'
```

- Expected: The help's audit-path list also names the folders that hold a guard file or agent (or the code refuses only the listed paths).
- Actual: Line 18 (help) lists only the exact paths; line 219 (code comment above $holders) shows ancestor folders such as .claude and .github count too.

## Re-audits

## Log

- 2026-10-09: filed proposed.
- 2026-10-09: proposed -> accepted.
