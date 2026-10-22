---
id: AF-0332
title: BL-2845 cost $7.50, 4.2 times the median task cost of $1.80: a full first run thrown away by a requeue, then an 80-turn second run and a rebase-conflict resolve
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:BL-2845:cost-outlier
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
# AF-0332 - BL-2845 cost $7.50, 4.2 times the median task cost of $1.80: a full first run thrown away by a requeue, then an 80-turn second run and a rebase-conflict resolve

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261021-140711-L9.log:2`: BL-2845 cost $7.50, 4.2 times the median task cost of $1.80: a full first run thrown away by a requeue, then an 80-turn second run and a rebase-conflict resolve.

## Evidence

Location: `logs/DarkFactory-20261021-140711-L9.log:2`

Runs (from the jsonl result lines): BL-2845-20261021-064209-L1 46 turns $2.54, then REQUEUE 07:50:15 'Waits on BL-2899 to BL-2907' (ten split tasks it depended on); BL-2845-20261021-140711-L9 80 turns $4.55, 9.2M cache-read tokens, 48 Bash calls; BL-2845-20261021-140711-L9-resolve 8 turns $0.41 for a rebase conflict. The $2.54 of the first run bought only a dependency discovery that planning could have made before the claim.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-19 -LogRoot Z:\repos\Curl.audit\20261022-013026\logs -CiRunsJson Z:\repos\Curl.audit\20261022-013026\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Sort-Object costUsd -Descending | Select-Object -First 5
```

- Expected: No task over 3 times the median costUsd (median 1.8006, so 5.40).
- Actual: BL-2845 7.4985, BL-2938 5.7798, BL-2970 5.602, BL-2924 5.5624, BL-2859 5.4413

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
