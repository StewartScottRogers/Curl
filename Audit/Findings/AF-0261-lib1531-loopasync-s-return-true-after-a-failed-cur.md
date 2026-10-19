---
id: AF-0261
title: Lib1531 LoopAsync's return true after a failed curl_multi_fdset can become false with no test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-18_1905.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1531.cs:LoopAsync-true:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1531.cs:102:true
task: BL-2772
tasks: BL-2772
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed: 2026-10-18
closed-how: mechanical
closed-by: 2026-10-18_1905.md
---
# AF-0261 - Lib1531 LoopAsync's return true after a failed curl_multi_fdset can become false with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib1531.cs:102`: Lib1531 LoopAsync's return true after a failed curl_multi_fdset can become false with no test failing. Reported by an auditor flagged unreliable in 2026-10-18_1045.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1531.cs:102`

LibTest sample, seed 0: survived. Original `return true;` after writing 'curl_multi_fdset() failed, code N.', mutated `return false;`. This flips the loop's continue-or-stop decision on an fdset failure, which changes how the port ends. No test reaches that branch with a check on what follows.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1531.cs:102:true -Member LoopAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-18 | 2026-10-18_1905.md | reproduces: no | Ran the -Site command: line 102, return true -> false killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-18: filed proposed.
- 2026-10-18: proposed -> accepted.
- 2026-10-18: accepted -> closed. Re-audit 2026-10-18_1905.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
