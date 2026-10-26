---
id: AF-0434
title: task-board.ps1 help says archive moves finished tasks but omits that only those completed -OlderThanDays (default 7) ago move
auditor: truthfulness
severity: Low
status: closed
reason: Re-audit 2026-10-25_1542.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-25_0941.md, 2026-10-25_1542.md).
key: truthfulness:.claude/skills/task-board/task-board.ps1:archive:false-help
reproduction: none
task: BL-3499
tasks: BL-3499
found: 2026-10-24
found-at: 456765a13655caedad49326c988f9e4eec86da8b
scorecard: 2026-10-24_2106.md
duplicate-of:
closed: 2026-10-25
closed-how: consecutive
closed-by: 2026-10-25_0941.md, 2026-10-25_1542.md
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

- 2026-10-25 | 2026-10-25_0941.md | reproduces: no | Ran the reproduction: it printed only task-board.ps1:125 ([int] $OlderThanDays = 7) and task-board.ps1:810 (AddDays(-$OlderThanDays)). The old help text 'archive  Move finished tasks into' no longer matches, because line 53 now reads 'archive  Move finished tasks completed -OlderThanDays (default 7) or more days ago into Done\<yyyy-MM-dd>\' and lines 55-57 add that a plain archive leaves the last week's Done tasks and that -OlderThanDays 0 moves them all.
- 2026-10-25 | 2026-10-25_1542.md | reproduces: no | Ran the reproduction: 'archive  Move finished tasks into' no longer matches; only task-board.ps1:125 ([int] $OlderThanDays = 7) and :810 (AddDays(-$OlderThanDays)) match. The help at task-board.ps1:53-58 now reads 'archive  Move finished tasks completed -OlderThanDays (default 7) or more days ago into Done\<yyyy-MM-dd>\ ... A plain 'archive' leaves the last week's Done tasks in place; -OlderThanDays 0 moves them all', which matches the code.

## Log

- 2026-10-24: filed proposed.
- 2026-10-25: proposed -> accepted.
- 2026-10-25: accepted -> closed. Re-audit 2026-10-25_1542.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-25_0941.md, 2026-10-25_1542.md).
