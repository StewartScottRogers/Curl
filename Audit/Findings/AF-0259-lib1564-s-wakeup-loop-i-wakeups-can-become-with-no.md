---
id: AF-0259
title: Lib1564's wakeup loop i < wakeups can become <= with no test failing, sending one wakeup too many per round
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-18_1905.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1564.cs:TestAsync-lt:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1564.cs:65:<
task: BL-2770
tasks: BL-2770
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed: 2026-10-18
closed-how: mechanical
closed-by: 2026-10-18_1905.md
---
# AF-0259 - Lib1564's wakeup loop i < wakeups can become <= with no test failing, sending one wakeup too many per round

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib1564.cs:65`: Lib1564's wakeup loop i < wakeups can become <= with no test failing, sending one wakeup too many per round. Reported by an auditor flagged unreliable in 2026-10-18_1045.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1564.cs:65`

LibTest sample, seed 0: survived. Original `for (int i = 0; i < wakeups; i++)`, mutated `<=`. A round meant to have no wakeup gets one, so curl_multi_poll returns early where upstream's lib1564 expects it to wait. No test pins the number of MultiWakeup calls per round.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1564.cs:65:< -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-18 | 2026-10-18_1905.md | reproduces: no | Ran the -Site command: line 65, i < wakeups -> <= killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-18: filed proposed.
- 2026-10-18: proposed -> accepted.
- 2026-10-18: accepted -> closed. Re-audit 2026-10-18_1905.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
