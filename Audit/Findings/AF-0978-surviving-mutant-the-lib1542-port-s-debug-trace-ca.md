---
id: AF-0978
title: Surviving mutant: the lib1542 port's debug trace can dump with the hex column (noHex true -> false)
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1542.cs:TestAsync-true:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1542.cs:49:true
task: none
tasks:
found: 2026-11-22
found-at: fdfc7d13ff07752cdeb7bf999a56dd653660b7a2
scorecard: 2026-11-22_1351.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0978 - Surviving mutant: the lib1542 port's debug trace can dump with the hex column (noHex true -> false)

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib1542.cs:49`: Surviving mutant: the lib1542 port's debug trace can dump with the hex column (noHex true -> false).

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1542.cs:49`

Invoke-MutationTest -Library Curl.Conformance.LibTest.UnitLibrary -MaxMutants 40 -Seed 891023: survived. Mutated 'new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: false, noHex: true)' to 'noHex: false'. The port's stderr trace would gain upstream's hex column, which lib1542's upstream source does not print, and no test pins the trace format. This is the same gap as AF-0952 (lib3033) and AF-0967 (lib655).

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1542.cs:49:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-22: filed proposed.
