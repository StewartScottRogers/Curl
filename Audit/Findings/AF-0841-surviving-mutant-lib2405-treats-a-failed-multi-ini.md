---
id: AF-0841
title: Surviving mutant: lib2405 treats a failed multi init as a passed empty-multi test
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib2405.cs:EmptyMultiTestAsync-false:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib2405.cs:161:false
task: none
tasks:
found: 2026-11-17
found-at: 10c240a4859c7868ec16b722cde6234f80cc8fcb
scorecard: 2026-11-17_0430.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0841 - Surviving mutant: lib2405 treats a failed multi init as a passed empty-multi test

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib2405.cs:161`: Surviving mutant: lib2405 treats a failed multi init as a passed empty-multi test.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib2405.cs:161`

return false; -> return true; after !steps.MultiInit(library, out multi) survived. A failed curl_multi_init would then be reported as a passing check and the program would go on.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib2405.cs:161:false -Member EmptyMultiTestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-17: filed proposed.
