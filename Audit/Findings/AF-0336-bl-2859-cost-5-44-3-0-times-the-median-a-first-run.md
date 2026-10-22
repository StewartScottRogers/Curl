---
id: AF-0336
title: BL-2859 cost $5.44, 3.0 times the median: a first run requeued on a dependency, then a 79-turn second run
auditor: process
severity: Low
status: accepted
reason:
key: process:logs:BL-2859:cost-outlier
reproduction: none
task: BL-3089
tasks: BL-3089
found: 2026-10-22
found-at: 167e765b2a009a4887fc971075283ef9d24a73a1
scorecard: 2026-10-22_0130.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0336 - BL-2859 cost $5.44, 3.0 times the median: a first run requeued on a dependency, then a 79-turn second run

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261021-140711-L5.log:54`: BL-2859 cost $5.44, 3.0 times the median: a first run requeued on a dependency, then a 79-turn second run.

## Evidence

Location: `logs/DarkFactory-20261021-140711-L5.log:54`

BL-2859-20261021-064209-L5: 23 turns, $1.01. Then REQUEUE 10:16:29 'Waits on BL-2955: Curl cannot reach a S...' (socks5unix). BL-2859-20261021-140711-L5: 79 turns, 29.5 min, $4.44, 9.5M cache-read tokens, 62 dotnet build/test mentions. DONE 14:54:07. The cost comes from a dependency found only after the claim, and from a long build-and-test loop for two server stand-ins.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-19 -LogRoot Z:\repos\Curl.audit\20261022-013026\logs -CiRunsJson Z:\repos\Curl.audit\20261022-013026\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2859'
```

- Expected: costUsd at most 3 times the median (5.40).
- Actual: claims=2, costUsd=5.4413, outcome=done

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
