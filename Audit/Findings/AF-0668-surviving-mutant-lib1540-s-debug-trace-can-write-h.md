---
id: AF-0668
title: Surviving mutant: lib1540's debug trace can write hex dumps (noHex false)
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-05_2025.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1540.cs:TestAsync-true:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1540.cs:51:true
task: BL-4867
tasks: BL-4856, BL-4867
found: 2026-11-04
found-at: 4749d66adb6a165fcd90aec741b5f49017616c28
scorecard: 2026-11-04_1442.md
duplicate-of:
closed: 2026-11-05
closed-how: mechanical
closed-by: 2026-11-05_2025.md
---
# AF-0668 - Surviving mutant: lib1540's debug trace can write hex dumps (noHex false)

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib1540.cs:51`: Surviving mutant: lib1540's debug trace can write hex dumps (noHex false). Reported by an auditor flagged unreliable in 2026-11-04_1442.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1540.cs:51`

Sampled mutant 'noHex: true' -> 'noHex: false' in 'new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true)' survived (sample -Library Curl.Conformance.LibTest.UnitLibrary -MaxMutants 40 -Seed 413592). Upstream lib1540 sets nohex, and the change alters every data line this port writes to stderr. No test pins the trace format. Caveat: on this line -Site ...:51:true resolves to the first 'true' (traceTime), not noHex, so the targeted command below does not mutate this site; the sample command does.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1540.cs:51:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived (sampled noHex mutant)

## Re-audits

- 2026-11-05 | 2026-11-05_0819.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site command: outcome survived. The site resolved line 51's first 'true', now traceTime ('traceTime: true' -> 'traceTime: false'). That mutant survives, so the TestAsync-true site in Lib1540 is still unchecked; the noHex argument the title names was not mutated. Runner's targeted mutation rerun on the clean audited commit: survived.
- 2026-11-05 | 2026-11-05_2025.md | reproduces: no | Ran the -Site reproduction: outcome killed (resolvedLine 51; it mutated traceTime). The noHex: true -> false mutant at the same line, applied by hand in a scratch worktree, is also killed: 1 failed (Lib1540_SetsItsCallbacksAndTraceAndReturnsTheTransfersCode). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-04: filed proposed.
- 2026-11-05: accepted -> closed. Re-audit 2026-11-05_2025.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
