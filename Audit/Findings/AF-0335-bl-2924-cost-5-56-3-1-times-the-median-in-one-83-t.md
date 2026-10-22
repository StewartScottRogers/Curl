---
id: AF-0335
title: BL-2924 cost $5.56, 3.1 times the median, in one 83-turn run plus a rebase-conflict resolve
auditor: process
severity: Low
status: closed
reason: Audit 2026-10-22_1120.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-3088) are Done at 608a75558667, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
key: process:logs:BL-2924:cost-outlier
reproduction: none
task: BL-3088
tasks: BL-3088
found: 2026-10-22
found-at: 167e765b2a009a4887fc971075283ef9d24a73a1
scorecard: 2026-10-22_0130.md
duplicate-of:
closed: 2026-10-22
closed-how: task-done-historical
closed-by: 2026-10-22_1120.md
---
# AF-0335 - BL-2924 cost $5.56, 3.1 times the median, in one 83-turn run plus a rebase-conflict resolve

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261021-064209-L4.log:216`: BL-2924 cost $5.56, 3.1 times the median, in one 83-turn run plus a rebase-conflict resolve.

## Evidence

Location: `logs/DarkFactory-20261021-064209-L4.log:216`

BL-2924-20261021-064209-L4: 83 turns, 29.1 min, $5.06, 10.9M cache-read tokens, 42 Bash calls and 74 dotnet build/test mentions. A long build-and-test loop for one unit-program port (unit1658). Then BL-2924-20261021-064209-L4-resolve: 12 turns, $0.51 for a rebase conflict. DONE 10:28:11. The cost comes from turn count multiplied by context size, not from retries.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-19 -LogRoot Z:\repos\Curl.audit\20261022-013026\logs -CiRunsJson Z:\repos\Curl.audit\20261022-013026\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2924'
```

- Expected: costUsd at most 3 times the median (5.40).
- Actual: claims=1, costUsd=5.5624, outcome=done

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
- 2026-10-22: accepted -> closed. Audit 2026-10-22_1120.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-3088) are Done at 608a75558667, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
