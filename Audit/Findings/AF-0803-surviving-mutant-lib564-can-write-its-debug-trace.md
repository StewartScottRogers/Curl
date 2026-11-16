---
id: AF-0803
title: Surviving mutant: lib564 can write its debug trace without the time stamps upstream's trace has
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib564.cs:TestAsync-true:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib564.cs:66:true
task: none
tasks:
found: 2026-11-15
found-at: 2a5b0f025b7c8fef02cb85fb555414235eb4e132
scorecard: 2026-11-15_1610.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0803 - Surviving mutant: lib564 can write its debug trace without the time stamps upstream's trace has

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib564.cs:66`: Surviving mutant: lib564 can write its debug trace without the time stamps upstream's trace has.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib564.cs:66`

The seed 770877 sample of Curl.Conformance.LibTest.UnitLibrary: 'new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true)' -> 'traceTime: false' in TestAsync survived. The program's standard-error trace lines then lose their time prefix. No test of Lib564 checks the trace text, so the configured trace format is unpinned.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib564.cs:66:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-15: filed proposed.
