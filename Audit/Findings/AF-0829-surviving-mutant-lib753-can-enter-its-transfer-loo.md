---
id: AF-0829
title: Surviving mutant: lib753 can enter its transfer loop after a failed first perform
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-17_0430.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:StartAndLoopAsync-and:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:120:&&
task: BL-5303
tasks: BL-5303
found: 2026-11-16
found-at: f28eba04920fb5d8dd2a1c70283f268a1891b7e5
scorecard: 2026-11-16_1333.md
duplicate-of:
closed: 2026-11-17
closed-how: mechanical
closed-by: 2026-11-17_0430.md
---
# AF-0829 - Surviving mutant: lib753 can enter its transfer loop after a failed first perform

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:120`: Surviving mutant: lib753 can enter its transfer loop after a failed first perform. Reported by an auditor flagged unreliable in 2026-11-16_1333.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:120`

Mutation sample (seed 413266): 'goesOn = goesOn && running > 0;' mutated to 'goesOn = goesOn || running > 0;' survived. With the mutant, a first curl_multi_perform that failed while a transfer is still running enters the swap/wait loop instead of stopping, and one with nothing running still turns once, so the program's output and exit code after a failed perform differ from upstream's lib753.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:120:&& -Member StartAndLoopAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-17 | 2026-11-17_0430.md | reproduces: no | Ran the -Site command: resolvedLine 120, 'goesOn && running > 0' -> 'goesOn || running > 0', outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-16: filed proposed.
- 2026-11-17: accepted -> closed. Re-audit 2026-11-17_0430.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
