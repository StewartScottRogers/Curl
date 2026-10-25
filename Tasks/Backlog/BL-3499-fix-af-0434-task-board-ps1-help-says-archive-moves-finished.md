---
id: BL-3499
title: Fix AF-0434: task-board.ps1 help says archive moves finished tasks but omits that only those completed -OlderThanDays (default 7) ago move
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [.claude]
requirement: none
created: 2026-10-25
completed:
---
# BL-3499 — Fix AF-0434: task-board.ps1 help says archive moves finished tasks but omits that only those completed -OlderThanDays (default 7) ago move

## Goal

The defect the audit office reported as AF-0434 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0434 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0434-task-board-ps1-help-says-archive-moves-finished-ta.md`.

Location: `.claude/skills/task-board/task-board.ps1:53`

Location: `.claude/skills/task-board/task-board.ps1:53`

The help's Commands list says 'archive  Move finished tasks into Done\<yyyy-MM-dd>\, one folder per day' and mentions only -WhenDoneIsLong. The parameter block sets '[int] $OlderThanDays = 7', and the archive branch moves only tasks whose completed date is on or before Today minus OlderThanDays. Otherwise it prints 'Nothing in Done was completed on or before <date>.' A plain 'archive' therefore leaves the last week's Done tasks in place. The help's only hint is the example '-OlderThanDays 0'.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path .claude/skills/task-board/task-board.ps1 -Pattern 'archive  Move finished tasks into', '\[int\] \$OlderThanDays = 7', 'AddDays\(-\$OlderThanDays\)' | ForEach-Object { '{0}:{1}' -f $_.Filename, $_.LineNumber }
```

- Expected: The archive help line states the default 7-day age limit (-OlderThanDays) next to the default.
- Actual: task-board.ps1:53 (help without the age limit), task-board.ps1:122 ($OlderThanDays = 7), task-board.ps1:805 (cutoff applied)

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-25: Created.
