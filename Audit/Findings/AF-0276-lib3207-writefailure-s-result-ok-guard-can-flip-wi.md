---
id: AF-0276
title: Lib3207.WriteFailure's result == Ok guard can flip with no test failing, so failures go unreported and successes are reported as failures
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-19_2134.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib3207.cs:WriteFailure-eq:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib3207.cs:137:==
task: BL-2803
tasks: BL-2803
found: 2026-10-18
found-at: e17dfce54cf6f2d08fc7c4abe0655128f237ff3b
scorecard: 2026-10-18_1905.md
duplicate-of:
closed: 2026-10-19
closed-how: mechanical
closed-by: 2026-10-19_2134.md
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

- 2026-10-19 | 2026-10-19_2134.md | reproduces: no | Ran the -Site command: Lib3207.cs:137:== in WriteFailure; outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-18: filed proposed.
- 2026-10-19: proposed -> accepted.
- 2026-10-19: accepted -> closed. Re-audit 2026-10-19_2134.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
