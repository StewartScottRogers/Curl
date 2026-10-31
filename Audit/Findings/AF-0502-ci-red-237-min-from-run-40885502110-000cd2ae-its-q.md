---
id: AF-0502
title: CI red 237 min from run 40885502110 (000cd2ae): its quality-gate fix BL-4083 was requeued behind BL-4082 for 3 h 22 min
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:40885502110:ci-red
reproduction: none
task: BL-4412
tasks: BL-4412
found: 2026-10-31
found-at: bfe08061f844163d0f86f77dc0af8971b7b1b179
scorecard: 2026-10-31_0358.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0502 - CI red 237 min from run 40885502110 (000cd2ae): its quality-gate fix BL-4083 was requeued behind BL-4082 for 3 h 22 min

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261029-071934-L1.log`: CI red 237 min from run 40885502110 (000cd2ae): its quality-gate fix BL-4083 was requeued behind BL-4082 for 3 h 22 min.

## Evidence

Location: `logs/DarkFactory-20261029-071934-L1.log`

ciRedSpells: 2026-10-29T14:07:29Z to 18:04:37Z, 237.13 min, runId 40885502110, headSha 000cd2ae (07:07-11:04 MST). At 07:19:12 the coordinator filed BL-4082 (regression SocketAction_TimeoutAgainstASilentServer_...) and BL-4083 (flaky job Quality gates (windows-latest) step Measure coverage, complexity and CRAP). It filed BL-4089 (SocketAction_ConnectCompletesAtOnceThenReadWaits_...) at 07:39:29. BL-4082 was Done at 07:24:08 and BL-4089 at 07:51:57. BL-4083 was claimed by L1 at 07:21:03 and requeued at 07:24:44: 'Needs Curl.Engine.UnitLibrary/EngineComposition.cs, held whole by BL-4082 (Doing); depends-on BL-4082'. It was not claimed again until L4 at 10:46:11 (shift 085044), was Done at 11:02:18, and CI went green at 11:04. ADR-1158 says a CI fix's run never sends it back to wait for another task, yet BL-4083 waited about 3 h 20 min after BL-4082 was already Done.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-29 -LogRoot Z:/repos/Curl.audit/20261031-035829/logs -CiRunsJson Z:/repos/Curl.audit/20261031-035829/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).ciRedSpells | ? runId -eq 40885502110; Select-String -Path Z:/repos/Curl.audit/20261031-035829/logs/DarkFactory-*-L*.log -Pattern 'BL-4083 (claim|REQUEUE|DONE)'
```

- Expected: A filed CI fix is claimed and finished within 30 minutes and never requeued behind another task.
- Actual: minutes 237.13; BL-4083 REQUEUE 07:24:44 behind BL-4082, reclaimed 10:46:11, DONE 11:02:18.

## Re-audits

## Log

- 2026-10-31: filed proposed.
- 2026-10-31: proposed -> accepted.
