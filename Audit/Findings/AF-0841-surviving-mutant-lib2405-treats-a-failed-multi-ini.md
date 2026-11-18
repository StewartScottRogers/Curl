---
id: AF-0841
title: Surviving mutant: lib2405 treats a failed multi init as a passed empty-multi test
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-18_0810.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib2405.cs:EmptyMultiTestAsync-false:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib2405.cs:161:false
task: BL-5361
tasks: BL-5361
found: 2026-11-17
found-at: 10c240a4859c7868ec16b722cde6234f80cc8fcb
scorecard: 2026-11-17_0430.md
duplicate-of:
closed: 2026-11-18
closed-how: mechanical
closed-by: 2026-11-18_0810.md
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

- 2026-11-18 | 2026-11-18_0810.md | reproduces: no | Ran the -Site command: Lib2405.cs:161:false [EmptyMultiTestAsync] resolvedLine 161, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-17: filed proposed.
- 2026-11-18: accepted -> closed. Re-audit 2026-11-18_0810.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
