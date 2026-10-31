---
id: BL-4412
title: Fix AF-0502: CI red 237 min from run 40885502110 (000cd2ae): its quality-gate fix BL-4083 was requeued behind BL-4082 for 3 h 22 min
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-31
completed:
---
# BL-4412 — Fix AF-0502: CI red 237 min from run 40885502110 (000cd2ae): its quality-gate fix BL-4083 was requeued behind BL-4082 for 3 h 22 min

## Goal

The cause of the factory behaviour the audit office reported as AF-0502 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0502 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0502-ci-red-237-min-from-run-40885502110-000cd2ae-its-q.md`.

Location: `logs/DarkFactory-20261029-071934-L1.log`

Location: `logs/DarkFactory-20261029-071934-L1.log`

ciRedSpells: 2026-10-29T14:07:29Z to 18:04:37Z, 237.13 min, runId 40885502110, headSha 000cd2ae (07:07-11:04 MST). At 07:19:12 the coordinator filed BL-4082 (regression SocketAction_TimeoutAgainstASilentServer_...) and BL-4083 (flaky job Quality gates (windows-latest) step Measure coverage, complexity and CRAP). It filed BL-4089 (SocketAction_ConnectCompletesAtOnceThenReadWaits_...) at 07:39:29. BL-4082 was Done at 07:24:08 and BL-4089 at 07:51:57. BL-4083 was claimed by L1 at 07:21:03 and requeued at 07:24:44: 'Needs Curl.Engine.UnitLibrary/EngineComposition.cs, held whole by BL-4082 (Doing); depends-on BL-4082'. It was not claimed again until L4 at 10:46:11 (shift 085044), was Done at 11:02:18, and CI went green at 11:04. ADR-1158 says a CI fix's run never sends it back to wait for another task, yet BL-4083 waited about 3 h 20 min after BL-4082 was already Done.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-29 -LogRoot Z:/repos/Curl.audit/20261031-035829/logs -CiRunsJson Z:/repos/Curl.audit/20261031-035829/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).ciRedSpells | ? runId -eq 40885502110; Select-String -Path Z:/repos/Curl.audit/20261031-035829/logs/DarkFactory-*-L*.log -Pattern 'BL-4083 (claim|REQUEUE|DONE)'
```

- Expected: A filed CI fix is claimed and finished within 30 minutes and never requeued behind another task.
- Actual: minutes 237.13; BL-4083 REQUEUE 07:24:44 behind BL-4082, reclaimed 10:46:11, DONE 11:02:18.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-31 (interactive): fix c3f18a294 pushed 11:04 Arizona. Cause: shift 20261030-060635's -Hours deadline passed at 14:07; its lanes finished past it (14:44), and the merge's CI-wait poll (Get-IdlePollDecision with the shift's end as deadline) always answered stop, silently, so BL-4369 and BL-4370 waited for the 15:22 hand-over. Now a ready CI fix (Get-ReadyCiFixTaskId) is not held by the deadline there (the usage stop still holds), and the next shift starts beside the merge at the first poll. Remaining: a CI fix filed while the last lanes run past the deadline still waits for them to end (on 10-30 that would be about 26 min). -TestHeartbeat 29 PASS 0 FAIL under both shells. Waits for a shift on the fix before its measurement can be taken.
- 2026-10-31 (interactive): measured with Measure-FactoryProcess.ps1 over a snapshot of Z:/repos/Curl.logs (files written since 2026-10-30 12:00; the one live run BL-4420-L1 left out as running) with a fresh 500-run CI list, at about 11:10 Arizona 2026-10-31. Since f54935070, filed CI fixes: BL-4359 filed 11:39, done 11:41; BL-4368 filed 14:02, done 14:07; BL-4378 claimed 03:02, done 03:12; but BL-4369 and BL-4370, filed 14:24 and 14:27 on 10-30 by the coordinator after its lanes had ended, were claimed only at 15:22 when the next shift started (58 min). None was requeued behind another task. Stays open: a CI fix filed with no lane running waits for the next shift.

## Log

- 2026-10-31: Created.
