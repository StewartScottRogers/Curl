---
id: AF-0919
title: Surviving mutant: the lib1515 port can go on after easy_init fails instead of stopping
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs:PerformAsync-false:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs:119:false
task: none
tasks:
found: 2026-11-20
found-at: e78a1e70bbba7541cf5f1abfedb0ef1c8ae8e2a0
scorecard: 2026-11-20_1740.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0919 - Surviving mutant: the lib1515 port can go on after easy_init fails instead of stopping

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs:119`: Surviving mutant: the lib1515 port can go on after easy_init fails instead of stopping.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs:119`

In Request.PerformAsync, `if (!steps.EasyInit(library, out CurlEasy? easy)) { return false; }` mutated to `return true;` survived Curl.Conformance.LibTest.UnitTests. The port then reports the failed request as done and carries on with the next one, where upstream's do_one_request stops; the program's result and stderr differ. No test drives lib1515 with an easy_init that fails.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs:119:false -Member PerformAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-20: filed proposed.
