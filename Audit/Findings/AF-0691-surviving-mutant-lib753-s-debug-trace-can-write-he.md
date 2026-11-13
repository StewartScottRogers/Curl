---
id: AF-0691
title: Surviving mutant: lib753's debug trace can write hex dumps (noHex false)
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:TestAsync-true:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:41:true
task: BL-4885
tasks: BL-4885
found: 2026-11-05
found-at: 4eba3c63638befcc6a63aaa31ddf5419922fa939
scorecard: 2026-11-05_2025.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0691 - Surviving mutant: lib753's debug trace can write hex dumps (noHex false)

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:41`: Surviving mutant: lib753's debug trace can write hex dumps (noHex false).

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:41`

Invoke-MutationTest (Curl.Conformance.LibTest.UnitLibrary, -MaxMutants 40, -Seed 982898) changed '... new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true));' to '... noHex: false));' and every LibTest.UnitTests test passed (survived). I confirmed this by hand in a scratch worktree: 1205/1205 passed. lib753's stderr trace would gain hex dump columns unnoticed, unlike lib500/1515/1522/1540/2700, whose noHex mutants are now killed. Note: the line has two 'true' sites. The -Site form takes the first (traceTime), so a site rerun mutates traceTime, not this noHex mutant.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:41:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-12 | 2026-11-12_2105.md | not re-audited | Ran the -Site command: outcome killed, but the operator 'true' resolved to the line's first occurrence, 'traceTime: true' -> 'traceTime: false', not the 'noHex: true' the finding names. The targeted run cannot reach the noHex occurrence, so it does not tell whether that mutant still survives. Not re-audited.

## Log

- 2026-11-05: filed proposed.
