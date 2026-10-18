---
id: AF-0259
title: Lib1564's wakeup loop i < wakeups can become <= with no test failing, sending one wakeup too many per round
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1564.cs:TestAsync-lt:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1564.cs:65:<
task: none
tasks:
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed:
closed-how:
closed-by:
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

## Log

- 2026-10-18: filed proposed.
- 2026-10-18: proposed -> accepted.
