---
id: AF-0918
title: Surviving mutant: the lib583 port can refuse a run given exactly three arguments
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-21_0114.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib583.cs:RunAsync-lt:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib583.cs:24:<
task: BL-5653
tasks: BL-5653
found: 2026-11-20
found-at: e78a1e70bbba7541cf5f1abfedb0ef1c8ae8e2a0
scorecard: 2026-11-20_1740.md
duplicate-of:
closed: 2026-11-21
closed-how: mechanical
closed-by: 2026-11-21_0114.md
---
# AF-0918 - Surviving mutant: the lib583 port can refuse a run given exactly three arguments

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib583.cs:24`: Surviving mutant: the lib583 port can refuse a run given exactly three arguments.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib583.cs:24`

`return context.Arguments.Count < 3 ? Task.FromResult(LibTestResult.FromCurlCode(LibTestErrorCodes.MajorBad)) : LibTestRun.WithMacroLibraryAsync(...)` mutated to `<= 3` survived Curl.Conformance.LibTest.UnitTests. With exactly three arguments the port then answers MajorBad without running, instead of running the transfer; no test runs lib583 at the boundary (Lib583_RemovesTheHandleAfterOnePerform passes more).

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib583.cs:24:< -Member RunAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-21 | 2026-11-21_0114.md | reproduces: no | Ran the -Site Curl.Conformance.LibTest.UnitLibrary/Lib583.cs:24:< command: resolvedLine 24, 'Count < 3' -> 'Count <= 3', outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-20: filed proposed.
- 2026-11-21: accepted -> closed. Re-audit 2026-11-21_0114.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
