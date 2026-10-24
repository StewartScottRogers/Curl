---
id: AF-0407
title: CI red 37 minutes on work/dark-factory from run 39986660283 (RunAsync_StalledBelowSpeedLimitForSpeedTime_Exits28WithCurlsMessage)
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:RunAsync_StalledBelowSpeedLimitForSpeedTime_Exits28WithCurlsMessage:ci-red
reproduction: none
task: BL-3454
tasks: BL-3454
found: 2026-10-23
found-at: 94da765e1b4eb040511b48ff68f7b42c5226e207
scorecard: 2026-10-23_2322.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0407 - CI red 37 minutes on work/dark-factory from run 39986660283 (RunAsync_StalledBelowSpeedLimitForSpeedTime_Exits28WithCurlsMessage)

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: CI red 37 minutes on work/dark-factory from run 39986660283 (RunAsync_StalledBelowSpeedLimitForSpeedTime_Exits28WithCurlsMessage).

## Evidence

Location: `logs/ci-runs.json`

ciRedSpells: start 2026-10-23T16:41:26Z, end 17:18:53Z, 37.45 min, run 39986660283 on commit 13c20167. The coordinator logged the failure at 09:42:23 local and filed BL-3332 at 09:48:56, 7 min after the red (DarkFactory-20261023-035126.log:87-90). L2 claimed BL-3332 at 09:49:19 and it was Done at 09:53:12, about 12 min after the red. CI stayed red until 10:18 local, waiting for the next CI run to finish green.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-23 -LogRoot Z:\repos\Curl.audit\20261023-232221\logs -CiRunsJson Z:\repos\Curl.audit\20261023-232221\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 39986660283
```

- Expected: no ciRedSpells entry over 30 minutes
- Actual: start 2026-10-23T16:41:26Z end 17:18:53Z minutes 37.45 runId 39986660283

## Re-audits

## Log

- 2026-10-23: filed proposed.
- 2026-10-24: proposed -> accepted.
