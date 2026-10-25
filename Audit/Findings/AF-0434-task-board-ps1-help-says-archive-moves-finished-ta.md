---
id: AF-0434
title: task-board.ps1 help says archive moves finished tasks but omits that only those completed -OlderThanDays (default 7) ago move
auditor: truthfulness
severity: Low
status: proposed
reason:
key: truthfulness:.claude/skills/task-board/task-board.ps1:archive:false-help
reproduction: none
task: none
tasks:
found: 2026-10-24
found-at: 456765a13655caedad49326c988f9e4eec86da8b
scorecard: 2026-10-24_2106.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0434 - task-board.ps1 help says archive moves finished tasks but omits that only those completed -OlderThanDays (default 7) ago move

## Summary

Low finding from the truthfulness auditor at `.claude/skills/task-board/task-board.ps1:53`: task-board.ps1 help says archive moves finished tasks but omits that only those completed -OlderThanDays (default 7) ago move.

## Evidence

Location: `.claude/skills/task-board/task-board.ps1:53`

The help's Commands list says 'archive  Move finished tasks into Done\<yyyy-MM-dd>\, one folder per day' and mentions only -WhenDoneIsLong. The parameter block sets '[int] $OlderThanDays = 7', and the archive branch moves only tasks whose completed date is on or before Today minus OlderThanDays. Otherwise it prints 'Nothing in Done was completed on or before <date>.' A plain 'archive' therefore leaves the last week's Done tasks in place. The help's only hint is the example '-OlderThanDays 0'.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path .claude/skills/task-board/task-board.ps1 -Pattern 'archive  Move finished tasks into', '\[int\] \$OlderThanDays = 7', 'AddDays\(-\$OlderThanDays\)' | ForEach-Object { '{0}:{1}' -f $_.Filename, $_.LineNumber }
```

- Expected: The archive help line states the default 7-day age limit (-OlderThanDays) next to the default.
- Actual: task-board.ps1:53 (help without the age limit), task-board.ps1:122 ($OlderThanDays = 7), task-board.ps1:805 (cutoff applied)

## Re-audits

## Log

- 2026-10-24: filed proposed.
