---
id: AF-0990
title: Surviving mutant: the lib1542 port's debug trace can stamp each line with the time (traceTime false -> true)
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1542.cs:TestAsync-false:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1542.cs:49:false
task: none
tasks:
found: 2026-11-22
found-at: 94c9744bc919995aff928a0378582601ddbbe990
scorecard: 2026-11-22_1817.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0990 - Surviving mutant: the lib1542 port's debug trace can stamp each line with the time (traceTime false -> true)

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib1542.cs:49`: Surviving mutant: the lib1542 port's debug trace can stamp each line with the time (traceTime false -> true).

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1542.cs:49`

Seeded mutation run: 'new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: false, noHex: true)' -> 'traceTime: true' survived. The port writes upstream's trace on standard error; with the mutant every trace line gets a timestamp prefix that upstream's lib1542 does not write. This is the same family as AF-0978 (noHex), but a different argument, and no test checks lib1542's trace lines.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1542.cs:49:false -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-22: filed proposed.
