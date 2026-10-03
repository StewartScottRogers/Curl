---
id: AF-0028
title: BL-1289 was claimed 6 times (134 minutes) before reaching Done
auditor: process
severity: Medium
status: accepted
reason: 
key: process:BL-1289:BL-1289:redone-work
task: BL-1363
found: 2026-10-03
found-at: 454d1d2abbb96213c945e91f0dc3d241bd40cc2d
scorecard: 2026-10-03_0623.md
closed:
closed-by:
---
# AF-0028 - BL-1289 was claimed 6 times (134 minutes) before reaching Done

## Summary

Medium finding from the process auditor at `logs/BL-1289-20261002-211047-L1.jsonl:1`: BL-1289 was claimed 6 times (134 minutes) before reaching Done. Reported by an auditor flagged unreliable in 2026-10-03_0623.md.

## Evidence

Location: `logs/BL-1289-20261002-211047-L1.jsonl:1`

Measure-FactoryProcess.ps1 -Since 2026-09-30 reports BL-1289 with claims=6, minutes=134.62, costUsd=4.19. Run logs exist for lanes L1 and L5. One run ended 'FACTORY: BLOCKED BL-1289 back in Backlog behind BL-1290: the per-chunk allocations still left are in Curl.Console, which BL-1290 holds'. The final run ended 'FACTORY: DONE BL-1289'. The task was queued while its remaining work overlapped BL-1290's touches. That sent it back to Backlog repeatedly. This is a log-based reading only; the 6 claims were not traced one by one. Also in the same period, waitOverlapMinutes equals laneIdleMinutes (3847 of 3847): all lane idle time was overlap waiting.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-09-30 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object claims -ge 3 | Select-Object id,claims
```

- Expected: No task claimed 3 or more times.
- Actual: BL-1289 claims=6

## Re-audits

## Log

- 2026-10-03: filed proposed.
- 2026-10-03: proposed -> accepted.
