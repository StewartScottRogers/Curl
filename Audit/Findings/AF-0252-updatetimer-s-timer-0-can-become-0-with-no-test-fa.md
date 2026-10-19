---
id: AF-0252
title: UpdateTimer's timer >= 0 can become > 0 with no test failing, so a fired timer's 0 ms re-report to CURLMOPT_TIMERFUNCTION is dropped
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-18_1905.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs:UpdateTimer-ge:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs:760:>=
task: BL-2763
tasks: BL-2763
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed: 2026-10-18
closed-how: mechanical
closed-by: 2026-10-18_1905.md
---
# AF-0252 - UpdateTimer's timer >= 0 can become > 0 with no test failing, so a fired timer's 0 ms re-report to CURLMOPT_TIMERFUNCTION is dropped

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs:760`: UpdateTimer's timer >= 0 can become > 0 with no test failing, so a fired timer's 0 ms re-report to CURLMOPT_TIMERFUNCTION is dropped. Reported by an auditor flagged unreliable in 2026-10-18_1045.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs:760`

Engine sample, seed 0: survived. Original `if (timer == reportedTimerMilliseconds && !(timerFired && timer >= 0))`, mutated `timer > 0`. After the timer fires with an unchanged 0 ms timeout, the original calls the application's timer callback again and the mutant returns without calling it. This is a decision visible to libcurl API users that no test pins.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs:760:>= -Member UpdateTimer -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-18 | 2026-10-18_1905.md | reproduces: no | Ran the -Site command: line 760, timer >= 0 -> > 0 killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-18: filed proposed.
- 2026-10-18: proposed -> accepted.
- 2026-10-18: accepted -> closed. Re-audit 2026-10-18_1905.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
