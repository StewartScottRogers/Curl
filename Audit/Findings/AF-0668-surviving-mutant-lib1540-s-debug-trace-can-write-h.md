---
id: AF-0668
title: Surviving mutant: lib1540's debug trace can write hex dumps (noHex false)
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1540.cs:TestAsync-true:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1540.cs:51:true
task: none
tasks:
found: 2026-11-04
found-at: 4749d66adb6a165fcd90aec741b5f49017616c28
scorecard: 2026-11-04_1442.md
duplicate-of:
closed:
closed-how:
closed-by:
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

## Log

- 2026-11-04: filed proposed.
