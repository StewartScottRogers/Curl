---
id: AF-0274
title: Lib552's debug trace noHex: true can become false with no test failing, changing its stderr dump
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib552.cs:TestAsync-true:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib552.cs:50:true
task: BL-2801
tasks: BL-2801
found: 2026-10-18
found-at: e17dfce54cf6f2d08fc7c4abe0655128f237ff3b
scorecard: 2026-10-18_1905.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0274 - Lib552's debug trace noHex: true can become false with no test failing, changing its stderr dump

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib552.cs:50`: Lib552's debug trace noHex: true can become false with no test failing, changing its stderr dump. Reported by an auditor flagged unreliable in 2026-10-18_1905.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib552.cs:50`

Mutant new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: false, noHex: true) -> noHex: false survived (sampled run, seed 0). The program's stderr trace switches to a hex dump. That is output bytes of the port the upstream case runner runs, and no test pins them.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib552.cs:50:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-10-18: filed proposed.
- 2026-10-19: proposed -> accepted.
