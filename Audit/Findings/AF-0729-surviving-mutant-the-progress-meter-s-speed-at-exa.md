---
id: AF-0729
title: Surviving mutant: the progress meter's speed at exactly one second can read as the maximum for huge sizes
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Output.UnitLibrary/ProgressMeterFields.cs:Speed-ge:surviving-mutant
reproduction: mutation Curl.Output.UnitLibrary/ProgressMeterFields.cs:103:>=
task: none
tasks:
found: 2026-11-12
found-at: acc2af773fb64825b3b57d9470bb6f96176fb127
scorecard: 2026-11-12_2105.md
duplicate-of:
closed:
closed-how:
closed-by:
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

## Log

- 2026-11-12: filed proposed.
