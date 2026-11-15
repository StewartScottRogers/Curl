---
id: AF-0794
title: Surviving mutant: the progress meter can be finished with the transfer's success inverted
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs:FollowFinishingMeterAsync-true:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs:117:true
task: BL-5245
tasks: BL-5245
found: 2026-11-15
found-at: 065474f7431e6647cda727e7f0fd5a25c9859a90
scorecard: 2026-11-15_1159.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0794 - Surviving mutant: the progress meter can be finished with the transfer's success inverted

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs:117`: Surviving mutant: the progress meter can be finished with the transfer's success inverted.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs:117`

Mutant 'progress?.FinishMeter(result?.IsSuccess == true)' -> '== false' survived (seed 5851). The meter would be told a successful transfer failed and a failed one succeeded, which decides the final progress-meter line the tool writes to stderr. No test pins FinishMeter's argument through FollowFinishingMeterAsync.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs:117:true -Member FollowFinishingMeterAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-15: filed proposed.
