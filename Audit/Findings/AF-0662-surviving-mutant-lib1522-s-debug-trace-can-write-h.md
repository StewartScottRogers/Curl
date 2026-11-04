---
id: AF-0662
title: Surviving mutant: lib1522's debug trace can write hex dumps (noHex false)
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1522.cs:TestAsync-true:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1522.cs:63:true
task: none
tasks:
found: 2026-11-04
found-at: 650d20c75108547e7bc74da2e86fbee9c49f7931
scorecard: 2026-11-04_0643.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0662 - Surviving mutant: lib1522's debug trace can write hex dumps (noHex false)

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib1522.cs:63`: Surviving mutant: lib1522's debug trace can write hex dumps (noHex false).

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1522.cs:63`

Mutant 'noHex: true' -> 'noHex: false' on its LibTestDebugTrace survived. The port's stderr trace would then carry hex dumps that upstream lib1522 does not write. No test pins that trace.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1522.cs:63:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-04: filed proposed.
- 2026-11-04: proposed -> accepted.
