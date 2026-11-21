---
id: AF-0937
title: Surviving mutant: the lib1501 port can fail a wait of exactly its blocking limit
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1501.cs:WithinBlockingLimit-le:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1501.cs:98:<=
task: none
tasks:
found: 2026-11-21
found-at: 57bece6c118995fac41401b81793a8395d3fb16d
scorecard: 2026-11-21_0835.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0937 - Surviving mutant: the lib1501 port can fail a wait of exactly its blocking limit

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib1501.cs:98`: Surviving mutant: the lib1501 port can fail a wait of exactly its blocking limit. Reported by an auditor flagged unreliable in 2026-11-21_0835.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1501.cs:98`

Seed 812510 sample: if (elapsed <= MaxBlockedMilliseconds) mutated to < survived; a pong of exactly the limit then fails the port. No test pins the boundary.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1501.cs:98:<= -Member WithinBlockingLimit -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-21: filed proposed.
