---
id: AF-0953
title: Surviving mutant: the lib1507 port can abort a loop at exactly its hang limit (> -> >=)
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-22_0210.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1507.cs:LoopAsync-gt:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1507.cs:95:>
task: BL-5722
tasks: BL-5722
found: 2026-11-21
found-at: 8d41979abc77036a1cf5ff183027379b16dc1eea
scorecard: 2026-11-21_1740.md
duplicate-of:
closed: 2026-11-22
closed-how: mechanical
closed-by: 2026-11-22_0210.md
---
# AF-0953 - Surviving mutant: the lib1507 port can abort a loop at exactly its hang limit (> -> >=)

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib1507.cs:95`: Surviving mutant: the lib1507 port can abort a loop at exactly its hang limit (> -> >=). Reported by an auditor flagged unreliable in 2026-11-21_1740.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1507.cs:95`

Line 95: if (clock.DifferenceMilliseconds(clock.Now(), start) > MultiPerformHangTimeoutMilliseconds) writes "ABORTING TEST, since it seems that it would have run forever." and returns; mutated to >= survived. The loop takes an injected LibTestClock, so the boundary is testable, but no test runs it at exactly the limit: the port can abort, with that stderr line, one tick earlier than upstream.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1507.cs:95:> -Member LoopAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-22 | 2026-11-22_0210.md | reproduces: no | Ran the -Site command: resolvedLine 95, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-21: filed proposed.
- 2026-11-22: accepted -> closed. Re-audit 2026-11-22_0210.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
