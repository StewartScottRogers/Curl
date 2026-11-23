---
id: AF-0967
title: Surviving mutant: the lib655 port's debug trace can dump with the hex column (noHex true -> false)
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib655.cs:TestAsync-true:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib655.cs:46:true
task: BL-5738
tasks: BL-5738
found: 2026-11-22
found-at: 9f9f37761162551e34c06d99ea4c7ebfb0168e83
scorecard: 2026-11-22_0210.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0967 - Surviving mutant: the lib655 port's debug trace can dump with the hex column (noHex true -> false)

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib655.cs:46`: Surviving mutant: the lib655 port's debug trace can dump with the hex column (noHex true -> false).

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib655.cs:46`

Seeded sample (seed 51850, -MaxMutants 40): the mutant at Lib655.cs:46 operator 'true' survived. Original: 'var trace = new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true);'; mutated: 'noHex: false'. upstream's lib655 traces without the hex column, so the port's standard error bytes would differ from the case's expected output, and no Lib655 unit test pins the trace format. This is the same gap AF-0952 records for Lib3033.cs:59, which still survives this audit.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib655.cs:46:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a test pins lib655's trace lines without the hex column
- Actual: outcome survived

## Re-audits

- 2026-11-22 | 2026-11-22_1351.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site command: survived at resolved line 46. As with AF-0952, the line reads traceTime: true, noHex: true, and the tool flipped the first 'true' (traceTime). No test pins lib655's trace format. Runner's targeted mutation rerun on the clean audited commit: survived.

## Log

- 2026-11-22: filed proposed.
