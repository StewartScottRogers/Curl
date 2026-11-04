---
id: AF-0669
title: Surviving mutant: lib500's debug trace can write hex dumps (noHex false)
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib500.cs:TestAsync-true:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib500.cs:50:true
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
# AF-0669 - Surviving mutant: lib500's debug trace can write hex dumps (noHex false)

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib500.cs:50`: Surviving mutant: lib500's debug trace can write hex dumps (noHex false). Reported by an auditor flagged unreliable in 2026-11-04_1442.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib500.cs:50`

Sampled mutant 'noHex: true' -> 'noHex: false' in 'new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true)' survived (sample -Seed 413592). Upstream lib500 sets nohex; the trace lines it writes change and no test pins them. Same caveat as Lib1540: -Site ...:50:true mutates the first 'true' (traceTime) on the line, not this noHex site.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib500.cs:50:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived (sampled noHex mutant)

## Re-audits

## Log

- 2026-11-04: filed proposed.
