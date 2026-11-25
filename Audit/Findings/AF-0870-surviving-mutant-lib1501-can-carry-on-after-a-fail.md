---
id: AF-0870
title: Surviving mutant: lib1501 can carry on after a failed first multi perform
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-19_0134.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1501.cs:LoopAsync-and:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1501.cs:79:&&
task: BL-5512
tasks: BL-5512
found: 2026-11-18
found-at: 4f902456d88c3fda856546dbe0c9a7674b1f04c4
scorecard: 2026-11-18_0810.md
duplicate-of:
closed: 2026-11-19
closed-how: mechanical
closed-by: 2026-11-19_0134.md
---
# AF-0870 - Surviving mutant: lib1501 can carry on after a failed first multi perform

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib1501.cs:79`: Surviving mutant: lib1501 can carry on after a failed first multi perform. Reported by an auditor flagged unreliable in 2026-11-18_0810.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1501.cs:79`

Mutant 'goesOn = goesOn && steps.AbortOnTestTimeout(...)' -> '||' survived Curl.Conformance.LibTest.UnitTests. When the first MultiPerformAsync fails (goesOn false), the mutant calls AbortOnTestTimeout, which answers true before the hang timeout, so the loop runs on with transfers still running instead of stopping with the failure's result. The lib1501 port's exit code and output can then differ from upstream, and no test makes its first perform fail.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1501.cs:79:&& -Member LoopAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-19 | 2026-11-19_0134.md | reproduces: no | Targeted run Curl.Conformance.LibTest.UnitLibrary/Lib1501.cs:79:&& (resolvedLine 79) gave outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-18: filed proposed.
- 2026-11-19: accepted -> closed. Re-audit 2026-11-19_0134.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
