---
id: AF-0619
title: Surviving mutant: lib2405's port can fail a waitfds count of exactly 1 without a test failing
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-03_2203.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib2405.cs:AddedHandleCheckPasses-gt:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib2405.cs:191:>
task: BL-4792
tasks: BL-4792
found: 2026-11-03
found-at: 7410f102c809d5232e4f9b210b3ac32486848c27
scorecard: 2026-11-03_1615.md
duplicate-of:
closed: 2026-11-03
closed-how: mechanical
closed-by: 2026-11-03_2203.md
---
# AF-0619 - Surviving mutant: lib2405's port can fail a waitfds count of exactly 1 without a test failing

## Summary

High finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib2405.cs:191`: Surviving mutant: lib2405's port can fail a waitfds count of exactly 1 without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_1615.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib2405.cs:191`

Mutant 'Failed(..., count > 1, "curl_multi_waitfds() returned > 1 count of waitfds: ...")' -> 'count >= 1' survived. lib2405.c allows one descriptor for an added handle. The mutant fails the program, with a different exit code and stderr line, whenever waitfds answers 1. The tests only drive counts of 0 and above 1.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib2405.cs:191:> -Member AddedHandleCheckPasses -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-03 | 2026-11-03_2203.md | reproduces: no | Ran Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib2405.cs:191:> -Member AddedHandleCheckPasses -ExcludeBaselineFailures -TimeoutSeconds 600: resolvedLine 191, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_2203.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
