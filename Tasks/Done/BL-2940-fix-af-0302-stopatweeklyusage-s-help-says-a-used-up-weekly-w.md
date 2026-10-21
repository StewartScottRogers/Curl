---
id: BL-2940
title: Fix AF-0302: -StopAtWeeklyUsage's help says a used-up weekly window raises the alarm; the shift waits it out with a notice
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2940 — Fix AF-0302: -StopAtWeeklyUsage's help says a used-up weekly window raises the alarm; the shift waits it out with a notice

## Goal

The defect the audit office reported as AF-0302 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0302 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0302-stopatweeklyusage-s-help-says-a-used-up-weekly-win.md`.

Location: `RunDarkFactory.ps1:464`

Location: `RunDarkFactory.ps1:464`

The parameter comment Get-Help shows for -StopAtWeeklyUsage, line 464: 'a used-up weekly window raises the alarm.' Wait-ForFreshSession (line 1790) traces the weekly reset and waits for it via Wait-ForNewSession -UsageOnly. Its comment says 'a notice, not the alarm, since running out is the plan working (BL-806)', and every path returns ''. So the 'if ($weekly) { ... Invoke-Alarm ... }' at line 5058 never fires. The script's own DESCRIPTION, line 130, says the opposite of line 464: 'a used-up weekly window is waited out the same way, with a notice rather than the alarm'.

Reproduction, from the finding:

Run from the repository root:

```powershell
$l = Get-Content RunDarkFactory.ps1; ($l | Select-String 'used-up weekly window raises the alarm').Line.Trim(); $s = ($l | Select-String '^function Wait-ForFreshSession').LineNumber; $l[$s..($s+24)] | Select-String '\breturn\b' | ForEach-Object { $_.Line.Trim() }
```

- Expected: No 'raises the alarm' line, or Wait-ForFreshSession returning a non-empty reason for a used-up weekly window.
- Actual: # runs it closer to empty (Stewart, 2026-09-28); a used-up weekly window raises the alarm.
if (-not $u) { return '' }
if (-not $u) { return '' }
if ($u.FiveHour -lt $StopAtUsage) { return '' }
return ''

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-21: Doing -> Done. Help comment for -StopAtWeeklyUsage now says the shift waits out a used-up weekly window with a notice, matching the DESCRIPTION
