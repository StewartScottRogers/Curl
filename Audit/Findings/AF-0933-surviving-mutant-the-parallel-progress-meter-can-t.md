---
id: AF-0933
title: Surviving mutant: the parallel progress meter can treat an unknown upload total as known
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:SumRunningTransfers-and:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:235:&&
task: none
tasks:
found: 2026-11-21
found-at: 57bece6c118995fac41401b81793a8395d3fb16d
scorecard: 2026-11-21_0835.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0933 - Surviving mutant: the parallel progress meter can treat an unknown upload total as known

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:235`: Surviving mutant: the parallel progress meter can treat an unknown upload total as known. Reported by an auditor flagged unreliable in 2026-11-21_0835.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:235`

Seed 812510 sample of Curl.Engine.UnitLibrary: totals.UploadKnown && transfer.UploadTotal != 0 mutated to || survived. UploadKnown starts true, so the mutant marks the run's upload size known even when a transfer's total is 0, changing the -Z progress meter's upload share written to standard error. No test runs the parallel meter with an unknown upload size.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:235:&& -Member SumRunningTransfers -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-21: filed proposed.
