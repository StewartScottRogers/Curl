---
id: AF-0840
title: Surviving mutant: lib2023 can carry on to its next requests after a failed easy init
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib2023.cs:RequestsOnOneHandleAsync-false:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib2023.cs:95:false
task: BL-5360
tasks: BL-5360
found: 2026-11-17
found-at: 10c240a4859c7868ec16b722cde6234f80cc8fcb
scorecard: 2026-11-17_0430.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0840 - Surviving mutant: lib2023 can carry on to its next requests after a failed easy init

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib2023.cs:95`: Surviving mutant: lib2023 can carry on to its next requests after a failed easy init.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib2023.cs:95`

return false; -> return true; after !steps.EasyInitOrMajorBad(...) survived. The caller then runs the next request block on a dead library instead of stopping, so the program's result and request count can change. No test drives a failing EasyInit through RequestsOnOneHandleAsync.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib2023.cs:95:false -Member RequestsOnOneHandleAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-17: filed proposed.
