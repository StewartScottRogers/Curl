---
id: AF-0680
title: Surviving mutant: lib2700's debug trace can drop its timestamps or write hex dumps unnoticed
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-05_2025.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib2700.cs:TestAsync-true:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib2700.cs:63:true
task: BL-4871
tasks: BL-4871
found: 2026-11-05
found-at: aea9f0110c59ad5209aafb9f0f985886a2cb9439
scorecard: 2026-11-05_0819.md
duplicate-of:
closed: 2026-11-05
closed-how: mechanical
closed-by: 2026-11-05_2025.md
---
# AF-0680 - Surviving mutant: lib2700's debug trace can drop its timestamps or write hex dumps unnoticed

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib2700.cs:63`: Surviving mutant: lib2700's debug trace can drop its timestamps or write hex dumps unnoticed.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib2700.cs:63`

Line 63: 'var trace = new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true);'. Both mutants of this line survive. The seed-843793 sample flipped noHex to false and it survived. The targeted -Site run flips the line's first true (traceTime) to false and it also survived. No Lib2700 test pins the trace format the port writes to standard error, which upstream's test2700 compares.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib2700.cs:63:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: The mutant is killed.
- Actual: survived

## Re-audits

- 2026-11-05 | 2026-11-05_2025.md | reproduces: no | Ran the -Site reproduction: traceTime: true -> false, outcome killed (resolvedLine 63), so the timestamps are checked. The noHex: true -> false mutant at the same line, applied by hand in a scratch worktree, is also killed: 1 failed (Lib2700_TracesWithTimesAndWithoutHex). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-05: filed proposed.
- 2026-11-05: accepted -> closed. Re-audit 2026-11-05_2025.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
