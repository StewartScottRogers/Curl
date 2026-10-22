---
id: AF-0302
title: -StopAtWeeklyUsage's help says a used-up weekly window raises the alarm; the shift waits it out with a notice
auditor: truthfulness
severity: Low
status: proposed
reason:
key: truthfulness:RunDarkFactory.ps1:StopAtWeeklyUsage:false-help
reproduction: none
task: none
tasks:
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

- 2026-10-22 | 2026-10-22_0130.md | not re-audited | overlaps planted defect PD-403 in RunDarkFactory.ps1, so the auditor's verdict (reproduces no) is set aside: Ran the reproduction. Select-String found no 'used-up weekly window raises the alarm' in RunDarkFactory.ps1, so the .Line.Trim() call failed on null. The help now says (RunDarkFactory.ps1:150-151) 'a used-up weekly window is waited out the same way, with a notice rather than the alarm', and the parameter comment (lines 525-526) says the same. Wait-ForFreshSession has moved to DarkFactory/OutOfTokens.ps1:180. It waits for the weekly reset via Wait-ForNewSession -UsageOnly with a trace notice and returns '', with no alarm, which matches the help.

## Log

- 2026-10-19: filed proposed.
