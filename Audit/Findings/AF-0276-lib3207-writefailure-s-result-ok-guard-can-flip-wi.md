---
id: AF-0276
title: Lib3207.WriteFailure's result == Ok guard can flip with no test failing, so failures go unreported and successes are reported as failures
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib3207.cs:WriteFailure-eq:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib3207.cs:137:==
task: none
tasks:
found: 2026-10-18
found-at: e17dfce54cf6f2d08fc7c4abe0655128f237ff3b
scorecard: 2026-10-18_1905.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0276 - Lib3207.WriteFailure's result == Ok guard can flip with no test failing, so failures go unreported and successes are reported as failures

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib3207.cs:137`: Lib3207.WriteFailure's result == Ok guard can flip with no test failing, so failures go unreported and successes are reported as failures. Reported by an auditor flagged unreliable in 2026-10-18_1905.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib3207.cs:137`

Mutant if (result == CurlCode.Ok) return; -> != survived (sampled run, seed 0). The thread's failure line on stderr is then written for successful transfers and never for failed ones. That is output bytes of the port, and no test pins them.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib3207.cs:137:== -Member WriteFailure -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-10-18: filed proposed.
