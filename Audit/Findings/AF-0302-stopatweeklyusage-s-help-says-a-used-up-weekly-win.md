---
id: AF-0302
title: -StopAtWeeklyUsage's help says a used-up weekly window raises the alarm; the shift waits it out with a notice
auditor: truthfulness
severity: Low
status: accepted
reason: 
key: truthfulness:RunDarkFactory.ps1:StopAtWeeklyUsage:false-help
reproduction: none
task: BL-2940
tasks: BL-2940
found: 2026-10-19
found-at: 1b3c70b1436b2d499099c5c6dd7bb119e11f5631
scorecard: 2026-10-19_2134.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0302 - -StopAtWeeklyUsage's help says a used-up weekly window raises the alarm; the shift waits it out with a notice

## Summary

Low finding from the truthfulness auditor at `RunDarkFactory.ps1:464`: -StopAtWeeklyUsage's help says a used-up weekly window raises the alarm; the shift waits it out with a notice.

## Evidence

Location: `RunDarkFactory.ps1:464`

The parameter comment Get-Help shows for -StopAtWeeklyUsage, line 464: 'a used-up weekly window raises the alarm.' Wait-ForFreshSession (line 1790) traces the weekly reset and waits for it via Wait-ForNewSession -UsageOnly. Its comment says 'a notice, not the alarm, since running out is the plan working (BL-806)', and every path returns ''. So the 'if ($weekly) { ... Invoke-Alarm ... }' at line 5058 never fires. The script's own DESCRIPTION, line 130, says the opposite of line 464: 'a used-up weekly window is waited out the same way, with a notice rather than the alarm'.

## Reproduction

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

## Re-audits

## Log

- 2026-10-19: filed proposed.
- 2026-10-21: proposed -> accepted.
