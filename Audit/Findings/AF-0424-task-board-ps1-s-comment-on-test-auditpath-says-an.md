---
id: AF-0424
title: task-board.ps1's comment on Test-AuditPath says an ancestor such as .claude is not an audit path; the function treats .claude, .github and .claude/skills as audit paths
auditor: truthfulness
severity: Low
status: closed
reason: Re-audit 2026-10-24_2106.md: the reproduction no longer reproduces.
key: truthfulness:.claude/skills/task-board/task-board.ps1:Test-AuditPath:false-statement
reproduction: none
task: BL-3472
tasks: BL-3472
found: 2026-10-24
found-at: d3ee244c64c5cb56d3fe0ca2ed9728e9383eb8ea
scorecard: 2026-10-24_0845.md
duplicate-of:
closed: 2026-10-24
closed-how: reliable-reaudit
closed-by: 2026-10-24_2106.md
---
# AF-0424 - task-board.ps1's comment on Test-AuditPath says an ancestor such as .claude is not an audit path; the function treats .claude, .github and .claude/skills as audit paths

## Summary

High finding from the truthfulness auditor at `.claude/skills/task-board/task-board.ps1:224`: task-board.ps1's comment on Test-AuditPath says an ancestor such as .claude is not an audit path; the function treats .claude, .github and .claude/skills as audit paths. Reported by an auditor flagged unreliable in 2026-10-24_0845.md.

## Evidence

Location: `.claude/skills/task-board/task-board.ps1:224`

Lines 220-225, the comment block documenting Test-AuditPath: 'An audit path is ... Takes a path ConvertTo-TouchPath has normalised. An ancestor such as .claude or * is not one: the hook and CI catch real writes, and refusing * would refuse every task filed without touches.' The code at lines 237-241 says the opposite for .claude: '# A folder that holds a guard file or an auditor or analyst agent (".claude", ".github", ".claude/skills" and the like) counts too' and returns $true when any guard file or .claude/agents/audit-x / gap-x starts with '<path>/'. Extracting the function and calling it gives: .claude -> True, .github -> True, .claude/skills -> True, * -> False, Audit -> True, Curl.Cli.UnitLibrary -> False. The script's own help (lines 25-26) agrees with the code ('or a folder that holds any of them (.claude, .claude/skills, .github and the like)'), so the comment is the false statement. It states a wrong rule about which touches are lane-eligible. An agent maintaining the guard could act on it and narrow the function, letting lanes take tasks that touch .claude or .github.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path .claude/skills/task-board/task-board.ps1 -Pattern 'An ancestor such as \.claude or \* is not one', 'A folder that holds a guard file' | ForEach-Object { '{0}:{1}' -f $_.Filename, $_.LineNumber }
```

- Expected: Only task-board.ps1:237 (the code's rule); no comment says .claude is not an audit path.
- Actual: task-board.ps1:224 and task-board.ps1:237: the comment says an ancestor such as .claude is not an audit path, and the code beside it counts it as one.

## Re-audits

- 2026-10-24 | 2026-10-24_2106.md | reproduces: no | Ran the reproduction: 'An ancestor such as .claude or * is not one' no longer matches. The only match is task-board.ps1:237, the new comment 'A folder that holds a guard file or an auditor or analyst agent (".claude", ".github", ".claude/skills" and the like) counts too'. That agrees with the $holders check on lines 239-241.

## Log

- 2026-10-24: filed proposed.
- 2026-10-24: proposed -> accepted.
- 2026-10-24: accepted -> closed. Re-audit 2026-10-24_2106.md: the reproduction no longer reproduces.
