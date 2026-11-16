---
id: AF-0802
title: Surviving mutant: lib573 can write its debug trace with hex dumps, unlike upstream's nohex trace
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib573.cs:RunAsync-true:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib573.cs:31:true
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
# AF-0802 - Surviving mutant: lib573 can write its debug trace with hex dumps, unlike upstream's nohex trace

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib573.cs:31`: Surviving mutant: lib573 can write its debug trace with hex dumps, unlike upstream's nohex trace.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib573.cs:31`

The seed 770877 sample of Curl.Conformance.LibTest.UnitLibrary: 'new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true)' -> 'noHex: false' in RunAsync survived. The program's standard-error trace then carries hex dumps that upstream's lib573 configuration leaves out. No test of Lib573 checks the trace text, so the configured trace format is unpinned.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib573.cs:31:true -Member RunAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-15: filed proposed.
