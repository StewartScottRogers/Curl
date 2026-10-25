---
id: BL-3472
title: Fix AF-0424: task-board.ps1's comment on Test-AuditPath says an ancestor such as .claude is not an audit path; the function treats .claude, .github and .claude/skills as audit paths
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [.claude]
requirement: none
created: 2026-10-24
completed:
---
# BL-3472 — Fix AF-0424: task-board.ps1's comment on Test-AuditPath says an ancestor such as .claude is not an audit path; the function treats .claude, .github and .claude/skills as audit paths

## Goal

The defect the audit office reported as AF-0424 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0424 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0424-task-board-ps1-s-comment-on-test-auditpath-says-an.md`.

Location: `.claude/skills/task-board/task-board.ps1:224`

Location: `.claude/skills/task-board/task-board.ps1:224`

Lines 220-225, the comment block documenting Test-AuditPath: 'An audit path is ... Takes a path ConvertTo-TouchPath has normalised. An ancestor such as .claude or * is not one: the hook and CI catch real writes, and refusing * would refuse every task filed without touches.' The code at lines 237-241 says the opposite for .claude: '# A folder that holds a guard file or an auditor or analyst agent (".claude", ".github", ".claude/skills" and the like) counts too' and returns $true when any guard file or .claude/agents/audit-x / gap-x starts with '<path>/'. Extracting the function and calling it gives: .claude -> True, .github -> True, .claude/skills -> True, * -> False, Audit -> True, Curl.Cli.UnitLibrary -> False. The script's own help (lines 25-26) agrees with the code ('or a folder that holds any of them (.claude, .claude/skills, .github and the like)'), so the comment is the false statement. It states a wrong rule about which touches are lane-eligible. An agent maintaining the guard could act on it and narrow the function, letting lanes take tasks that touch .claude or .github.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path .claude/skills/task-board/task-board.ps1 -Pattern 'An ancestor such as \.claude or \* is not one', 'A folder that holds a guard file' | ForEach-Object { '{0}:{1}' -f $_.Filename, $_.LineNumber }
```

- Expected: Only task-board.ps1:237 (the code's rule); no comment says .claude is not an audit path.
- Actual: task-board.ps1:224 and task-board.ps1:237: the comment says an ancestor such as .claude is not an audit path, and the code beside it counts it as one.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
