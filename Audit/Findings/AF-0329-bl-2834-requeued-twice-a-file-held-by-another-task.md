---
id: AF-0329
title: BL-2834 requeued twice: a file held by another task's whole-library touches, then a requeue only to wait for CI
auditor: process
severity: Medium
status: accepted
reason:
key: process:logs:BL-2834:redone-work
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
# AF-0329 - BL-2834 requeued twice: a file held by another task's whole-library touches, then a requeue only to wait for CI

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261021-064209-L5.log:418`: BL-2834 requeued twice: a file held by another task's whole-library touches, then a requeue only to wait for CI.

## Evidence

Location: `logs/DarkFactory-20261021-064209-L5.log:418`

Claim 1 (064209-L5): 13:00:22 to 13:04:07, 25 turns. At 13:05:59 REQUEUE 'Needs Curl.Engine.UnitLibrary/EngineComposition.cs ..., which BL-2921 in Doing holds as the whole Curl.Engine.UnitLibrary'. A whole-project touches on BL-2921 serialised it, which CLAUDE.md forbids ('never a whole project it only adds to'). Claim 2 (150011-L3): 16:58:20 to 17:19:51, 55 turns. At 17:20:48 REQUEUE 'Code committed; only the CI-on-Linux-and-macOS criterion is left'. A third claim, logged only in the task Log, then confirmed CI and moved it to Done. Total cost $4.93. The second requeue exists only because a lane cannot see CI for its own push.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-19 -LogRoot Z:\repos\Curl.audit\20261022-013026\logs -CiRunsJson Z:\repos\Curl.audit\20261022-013026\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2834'; Select-String -Path Z:\repos\Curl.audit\20261022-013026\logs\DarkFactory-*-L*.log -Pattern 'BL-2834 +(claim|REQUEUE|DONE)'
```

- Expected: BL-2834 requeued fewer than twice.
- Actual: claims=2, costUsd=4.9325, 2 REQUEUE lines (13:05:59 outside touches; 17:20:48 waiting on CI).

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
