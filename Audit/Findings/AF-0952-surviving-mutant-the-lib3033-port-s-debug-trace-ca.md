---
id: AF-0952
title: Surviving mutant: the lib3033 port's debug trace can dump with the hex column (noHex true -> false)
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib3033.cs:TestAsync-true:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib3033.cs:59:true
task: BL-5731
tasks: BL-5721, BL-5731
found: 2026-11-21
found-at: 8d41979abc77036a1cf5ff183027379b16dc1eea
scorecard: 2026-11-21_1740.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0952 - Surviving mutant: the lib3033 port's debug trace can dump with the hex column (noHex true -> false)

## Summary

High finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib3033.cs:59`: Surviving mutant: the lib3033 port's debug trace can dump with the hex column (noHex true -> false). Reported by an auditor flagged unreliable in 2026-11-21_1740.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib3033.cs:59`

Line 59: var trace = new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true); mutated to noHex: false survived the whole Curl.Conformance.LibTest.UnitTests run. LibTestDebugTrace.DebugDump (LibTestDebugTrace.cs:58-63) writes 16-byte rows with a hex column instead of 64-byte text rows when noHex is false, so the port's stderr bytes change and no test pins lib3033's trace output.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib3033.cs:59:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-22 | 2026-11-22_0210.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site command: resolvedLine 59, outcome survived. Line 59 is still 'var trace = new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true);' and no test pins the trace format. Runner's targeted mutation rerun on the clean audited commit: survived.

## Log

- 2026-11-21: filed proposed.
