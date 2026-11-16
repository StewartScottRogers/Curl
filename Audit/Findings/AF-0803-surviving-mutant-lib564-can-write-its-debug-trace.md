---
id: AF-0803
title: Surviving mutant: lib564 can write its debug trace without the time stamps upstream's trace has
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-16_0514.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib564.cs:TestAsync-true:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib564.cs:66:true
task: BL-5254
tasks: BL-5254
found: 2026-11-15
found-at: 2a5b0f025b7c8fef02cb85fb555414235eb4e132
scorecard: 2026-11-15_1610.md
duplicate-of:
closed: 2026-11-16
closed-how: mechanical
closed-by: 2026-11-16_0514.md
---
# AF-0803 - Surviving mutant: lib564 can write its debug trace without the time stamps upstream's trace has

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib564.cs:66`: Surviving mutant: lib564 can write its debug trace without the time stamps upstream's trace has.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib564.cs:66`

The seed 770877 sample of Curl.Conformance.LibTest.UnitLibrary: 'new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true)' -> 'traceTime: false' in TestAsync survived. The program's standard-error trace lines then lose their time prefix. No test of Lib564 checks the trace text, so the configured trace format is unpinned.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib564.cs:66:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-16 | 2026-11-16_0514.md | not re-audited | overlaps planted defect PD-101 in Curl.Cryptography.UnitLibrary/HmacRipemd160.cs, so the auditor's verdict (reproduces yes) is set aside: still reported
- 2026-11-16 | 2026-11-16_0514.md | reproduces: no | Ran the -Site command: Lib564.cs:66 resolved to the first `true` (traceTime: true -> false), and it was killed. Separately, the sampled run's noHex: true -> false mutant on the same line survived; that is filed as its own finding. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-15: filed proposed.
- 2026-11-16: accepted -> closed. Re-audit 2026-11-16_0514.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
