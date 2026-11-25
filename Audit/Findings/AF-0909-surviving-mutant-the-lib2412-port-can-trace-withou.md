---
id: AF-0909
title: Surviving mutant: the lib2412 port can trace without the time prefix
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-20_1740.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib2412.cs:TestAsync-true:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib2412.cs:54:true
task: BL-5624
tasks: BL-5624
found: 2026-11-20
found-at: 1ce730765b795e99d4d034faf03432b41b5ec6a4
scorecard: 2026-11-20_0456.md
duplicate-of:
closed: 2026-11-20
closed-how: mechanical
closed-by: 2026-11-20_1740.md
---
# AF-0909 - Surviving mutant: the lib2412 port can trace without the time prefix

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib2412.cs:54`: Surviving mutant: the lib2412 port can trace without the time prefix.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib2412.cs:54`

Invoke-MutationTest -Library Curl.Conformance.LibTest.UnitLibrary -MaxMutants 40 -Seed 912464: 'new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true)' mutated to traceTime: false survived. Each trace line then loses its 'HH:mm:ss.ffffff ' prefix (LibTestDebugTrace.cs:130), so the port's standard error changes and no test pins it.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib2412.cs:54:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-20 | 2026-11-20_1740.md | reproduces: no | Ran the -Site command: Lib2412.cs:54:true (TestAsync), resolvedLine 54, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-20: filed proposed.
- 2026-11-20: accepted -> closed. Re-audit 2026-11-20_1740.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
