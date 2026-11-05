---
id: AF-0670
title: Surviving mutant: lib1515's debug trace can write hex dumps (noHex false)
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs:PerformAsync-true:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs:124:true
task: BL-4858
tasks: BL-4858
found: 2026-11-04
found-at: 4749d66adb6a165fcd90aec741b5f49017616c28
scorecard: 2026-11-04_1442.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0670 - Surviving mutant: lib1515's debug trace can write hex dumps (noHex false)

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs:124`: Surviving mutant: lib1515's debug trace can write hex dumps (noHex false). Reported by an auditor flagged unreliable in 2026-11-04_1442.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs:124`

Sampled mutant 'noHex: true' -> 'noHex: false' in 'new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true)' survived (sample -Seed 413592). No test pins lib1515's trace format. Same caveat: -Site ...:124:true mutates the line's first 'true' (traceTime).

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs:124:true -Member PerformAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived (sampled noHex mutant)

## Re-audits

## Log

- 2026-11-04: filed proposed.
