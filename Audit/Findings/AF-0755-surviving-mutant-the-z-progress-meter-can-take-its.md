---
id: AF-0755
title: Surviving mutant: the -Z progress meter can take its speed window from an unset sample
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-14_1801.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:Speed-eq:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:282:==
task: BL-5204
tasks: BL-5204
found: 2026-11-14
found-at: fc4d1268bd4c1bce123dba90334b07e141be698a
scorecard: 2026-11-14_1335.md
duplicate-of:
closed: 2026-11-14
closed-how: mechanical
closed-by: 2026-11-14_1801.md
---
# AF-0755 - Surviving mutant: the -Z progress meter can take its speed window from an unset sample

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:282`: Surviving mutant: the -Z progress meter can take its speed window from an unset sample.

## Evidence

Location: `Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:282`

Mutant 'speedSamplesWrapped |= nextSpeedSample == 0;' -> '!= 0' survived. After the first sample the meter would compute the speed from speedSamples[next], a default (0,0,0) entry, instead of from the run's start, so the Speed column of the parallel progress meter changes. No test pins the speed before ten samples.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:282:== -Member Speed -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-14 | 2026-11-14_1801.md | reproduces: no | Ran the -Site command: ParallelProgressMeter.cs:282 'nextSpeedSample == 0' -> '!=', outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-14: filed proposed.
- 2026-11-14: accepted -> closed. Re-audit 2026-11-14_1801.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
