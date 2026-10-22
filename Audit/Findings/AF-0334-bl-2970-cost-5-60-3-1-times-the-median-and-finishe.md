---
id: AF-0334
title: BL-2970 cost $5.60, 3.1 times the median, and finished nothing itself: it was deferred and split
auditor: process
severity: Low
status: closed
reason: Audit 2026-10-22_1120.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-3087) are Done at 608a75558667, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
key: process:logs:BL-2970:cost-outlier
reproduction: none
task: BL-3087
tasks: BL-3087
found: 2026-10-22
found-at: 167e765b2a009a4887fc971075283ef9d24a73a1
scorecard: 2026-10-22_0130.md
duplicate-of:
closed: 2026-10-22
closed-how: task-done-historical
closed-by: 2026-10-22_1120.md
---
# AF-0334 - BL-2970 cost $5.60, 3.1 times the median, and finished nothing itself: it was deferred and split

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261021-064209-L1.log:311`: BL-2970 cost $5.60, 3.1 times the median, and finished nothing itself: it was deferred and split.

## Evidence

Location: `logs/DarkFactory-20261021-064209-L1.log:311`

BL-2970-20261021-064209-L1: 72 turns, $4.27, 7.4M cache-read tokens. It ended in a requeue for a file outside its touches, with the work stashed. BL-2970-20261021-140711-L8: 13 turns, $0.87; its stash did not apply cleanly. BL-2970-20261021-150011-L2: 5 turns, $0.46. Both later runs ended in cost-cap requeues. The work was carried over to BL-3011, which was PARKED at 20:58:14 when its push kept being refused.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-19 -LogRoot Z:\repos\Curl.audit\20261022-013026\logs -CiRunsJson Z:\repos\Curl.audit\20261022-013026\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2970'
```

- Expected: costUsd at most 3 times the median (5.40).
- Actual: claims=3, costUsd=5.602, outcome=requeued

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
- 2026-10-22: accepted -> closed. Audit 2026-10-22_1120.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-3087) are Done at 608a75558667, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
