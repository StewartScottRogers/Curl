---
id: AF-0333
title: BL-2938 cost $5.78, 3.2 times the median: a 60-turn first run whose stash did not apply on the second claim
auditor: process
severity: Low
status: proposed
reason:
key: process:logs:BL-2938:cost-outlier
reproduction: none
task: none
tasks:
found: 2026-10-22
found-at: 167e765b2a009a4887fc971075283ef9d24a73a1
scorecard: 2026-10-22_0130.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0333 - BL-2938 cost $5.78, 3.2 times the median: a 60-turn first run whose stash did not apply on the second claim

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261021-140711-L1.log:2`: BL-2938 cost $5.78, 3.2 times the median: a 60-turn first run whose stash did not apply on the second claim.

## Evidence

Location: `logs/DarkFactory-20261021-140711-L1.log:2`

BL-2938-20261021-064209-L3: 60 turns, $3.12. Its work was stashed, then REQUEUE 10:17:17 'Waits on BL-2956'. BL-2938-20261021-140711-L1: at 14:07:43, 'stash d6503b8f from an earlier run did not apply cleanly'. That run took 47 turns ($2.20), plus a resolve run of 9 turns ($0.46), DONE 14:43:48. More than half the cost was the first run, whose stash could not be applied directly.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-19 -LogRoot Z:\repos\Curl.audit\20261022-013026\logs -CiRunsJson Z:\repos\Curl.audit\20261022-013026\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2938'
```

- Expected: costUsd at most 3 times the median (5.40).
- Actual: claims=2, costUsd=5.7798, outcome=done

## Re-audits

## Log

- 2026-10-22: filed proposed.
