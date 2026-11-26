---
id: AF-0992
title: Surviving mutant: lib530's completion check can start out as success (success = false -> true), hiding 'Error getting file.'
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs:CheckForCompletion-false:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs:209:false
task: BL-5794
tasks: BL-5794
found: 2026-11-22
found-at: 94c9744bc919995aff928a0378582601ddbbe990
scorecard: 2026-11-22_1817.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0992 - Surviving mutant: lib530's completion check can start out as success (success = false -> true), hiding 'Error getting file.'

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs:209`: Surviving mutant: lib530's completion check can start out as success (success = false -> true), hiding 'Error getting file.'.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs:209`

Seeded mutation run: in CheckForCompletion (t530_checkForCompletion), 'success = false;' -> 'success = true;' survived. When no message is read, the mutant reports success, so the caller's 'if (!success) { WriteMessage("Error getting file."); steps.Result = MajorBad; }' is skipped on that path. No test drives a completion check that reads no message.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs:209:false -Member CheckForCompletion -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-25 | 2026-11-25_1306.md | not re-audited | Ran the -Site command: outcome site-missing. CheckForCompletion now starts with 'bool? success = null' (Lib530Run.cs:208); there is no 'success = false' site. Not re-audited.

## Log

- 2026-11-22: filed proposed.
