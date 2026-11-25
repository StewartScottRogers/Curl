---
id: AF-0936
title: Surviving mutant: the lib526-family port can clean up its handles when upstream does not
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-21_1740.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:CleansUpTheHandles-and:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:147:&&
task: BL-5695
tasks: BL-5695
found: 2026-11-21
found-at: 57bece6c118995fac41401b81793a8395d3fb16d
scorecard: 2026-11-21_0835.md
duplicate-of:
closed: 2026-11-21
closed-how: mechanical
closed-by: 2026-11-21_1740.md
---
# AF-0936 - Surviving mutant: the lib526-family port can clean up its handles when upstream does not

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:147`: Surviving mutant: the lib526-family port can clean up its handles when upstream does not. Reported by an auditor flagged unreliable in 2026-11-21_0835.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:147`

Seed 812510 sample of Curl.Conformance.LibTest.UnitLibrary: (testNumber == 527 && steps.Result != CurlCode.Ok) mutated to || survived, so lib527 always cleans up and the other ports clean up after any failure, unlike upstream lib526.c. No test drives this branch both ways.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:147:&& -Member CleansUpTheHandles -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-21 | 2026-11-21_1740.md | reproduces: no | Ran the -Site command: resolvedLine 147, mutant 'testNumber is 526 or 528 or 532 || (...)' killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-21: filed proposed.
- 2026-11-21: accepted -> closed. Re-audit 2026-11-21_1740.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
