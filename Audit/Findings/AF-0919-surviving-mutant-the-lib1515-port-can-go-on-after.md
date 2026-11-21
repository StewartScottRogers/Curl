---
id: AF-0919
title: Surviving mutant: the lib1515 port can go on after easy_init fails instead of stopping
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-21_0114.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs:PerformAsync-false:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs:119:false
task: BL-5654
tasks: BL-5654
found: 2026-11-20
found-at: e78a1e70bbba7541cf5f1abfedb0ef1c8ae8e2a0
scorecard: 2026-11-20_1740.md
duplicate-of:
closed: 2026-11-21
closed-how: mechanical
closed-by: 2026-11-21_0114.md
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

- 2026-11-21 | 2026-11-21_0114.md | reproduces: no | Ran the -Site Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs:119:false command: resolvedLine 119, 'return false;' -> 'return true;', outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-20: filed proposed.
- 2026-11-21: accepted -> closed. Re-audit 2026-11-21_0114.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
