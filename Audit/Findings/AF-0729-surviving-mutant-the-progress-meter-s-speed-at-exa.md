---
id: AF-0729
title: Surviving mutant: the progress meter's speed at exactly one second can read as the maximum for huge sizes
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Output.UnitLibrary/ProgressMeterFields.cs:Speed-ge:surviving-mutant
reproduction: mutation Curl.Output.UnitLibrary/ProgressMeterFields.cs:103:>=
task: BL-5088
tasks: BL-5088
found: 2026-11-12
found-at: acc2af773fb64825b3b57d9470bb6f96176fb127
scorecard: 2026-11-12_2105.md
duplicate-of:
closed: 2026-11-13
closed-how: mechanical
closed-by: 2026-11-13_2321.md
---
# AF-0729 - Surviving mutant: the progress meter's speed at exactly one second can read as the maximum for huge sizes

## Summary

Medium finding from the quality auditor at `Curl.Output.UnitLibrary/ProgressMeterFields.cs:103`: Surviving mutant: the progress meter's speed at exactly one second can read as the maximum for huge sizes. Reported by an auditor flagged unreliable in 2026-11-12_2105.md.

## Evidence

Location: `Curl.Output.UnitLibrary/ProgressMeterFields.cs:103`

Mutant 'microseconds >= 1000000' -> '> 1000000' survived. For sizes at or above long.MaxValue / 1000000 the original gives size / 1 at exactly 1,000,000 microseconds and the mutant gives long.MaxValue, a different speed column. The pure function's boundary is untested.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Output.UnitLibrary/ProgressMeterFields.cs:103:>= -Member Speed -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-13 | 2026-11-13_2321.md | reproduces: no | Ran the -Site command: outcome killed (microseconds >= 1000000 -> >). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-12: filed proposed.
- 2026-11-13: proposed -> accepted.
- 2026-11-13: accepted -> closed. Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
