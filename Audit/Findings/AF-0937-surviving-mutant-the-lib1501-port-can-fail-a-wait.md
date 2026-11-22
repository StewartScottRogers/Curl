---
id: AF-0937
title: Surviving mutant: the lib1501 port can fail a wait of exactly its blocking limit
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-21_1740.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1501.cs:WithinBlockingLimit-le:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1501.cs:98:<=
task: BL-5696
tasks: BL-5696
found: 2026-11-21
found-at: 57bece6c118995fac41401b81793a8395d3fb16d
scorecard: 2026-11-21_0835.md
duplicate-of:
closed: 2026-11-21
closed-how: mechanical
closed-by: 2026-11-21_1740.md
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

- 2026-11-21 | 2026-11-21_1740.md | reproduces: no | Ran the -Site command: line 98 is now a doc comment, and the tool resolved to line 103 in the same member WithinBlockingLimit. Mutant 'elapsed < MaxBlockedMilliseconds' killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-21: filed proposed.
- 2026-11-21: accepted -> closed. Re-audit 2026-11-21_1740.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
