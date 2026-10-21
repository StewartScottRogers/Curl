---
id: AF-0295
title: Lib1553's debug trace traceTime: true can become false with no test failing, dropping the timestamps from its stderr trace
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1553.cs:TestAsync-true:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1553.cs:72:true
task: BL-2933
tasks: BL-2933
found: 2026-10-19
found-at: 1b3c70b1436b2d499099c5c6dd7bb119e11f5631
scorecard: 2026-10-19_2134.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0295 - Lib1553's debug trace traceTime: true can become false with no test failing, dropping the timestamps from its stderr trace

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib1553.cs:72`: Lib1553's debug trace traceTime: true can become false with no test failing, dropping the timestamps from its stderr trace.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1553.cs:72`

Sampled mutant (seed 0) survived: new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true) => traceTime: false. The trace lib1553 writes to stderr would lose the time prefix upstream's debug callback writes, and no test checks Lib1553's trace text. This is the same kind of gap as AF-0274 (Lib552 noHex), in another port.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1553.cs:72:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-19: filed proposed.
- 2026-10-21: proposed -> accepted.
