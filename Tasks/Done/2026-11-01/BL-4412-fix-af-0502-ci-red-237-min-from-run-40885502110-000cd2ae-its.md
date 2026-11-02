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
completed: 2026-11-01
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

- [x] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes
- 2026-11-01 08:45 Arizona (interactive session): measured after ec555bb52 (landed 2026-10-31 18:45:22 Arizona). Window 18:45 to 08:45 Arizona: one shift, 20261101-072514 (coordinator and lanes L1-L4 in `Z:/repos/Curl.logs/DarkFactory-20261101-072514*.log`), and the 16 work/dark-factory CI runs created since (41267244579 to 41320132766, from a fresh `gh run list`). Red spells: 0 (every run success, or cancelled when superseded by a newer push: 41267244579, 41316890757, 41317208860, 41317362391). CI fixes filed: 0, so none waited or was requeued; requeued=0 on every lane. The CI watch ran inside the merge wait as ec555bb52 intends: coordinator line 08:25 `ci run 41318615482 success on 1f344d9a: nothing to file`, and the merge (PR #272) went through at 08:34. The finding's expected result holds for this window. Not exercised: a CI failure arriving during a merge wait, the case ec555bb52 adds (-TestHeartbeat covers it). Criterion 2: CI run 41320132766 on 61a5b2d97, which holds ec555bb52, built and ran the fast tests green on Windows, Linux and macOS. AF-0502 closes only on the process auditor's re-audit.

- 2026-10-31 (interactive): fix c3f18a294 pushed 11:04 Arizona. Cause: shift 20261030-060635's -Hours deadline passed at 14:07; its lanes finished past it (14:44), and the merge's CI-wait poll (Get-IdlePollDecision with the shift's end as deadline) always answered stop, silently, so BL-4369 and BL-4370 waited for the 15:22 hand-over. Now a ready CI fix (Get-ReadyCiFixTaskId) is not held by the deadline there (the usage stop still holds), and the next shift starts beside the merge at the first poll. Remaining: a CI fix filed while the last lanes run past the deadline still waits for them to end (on 10-30 that would be about 26 min). -TestHeartbeat 29 PASS 0 FAIL under both shells. Waits for a shift on the fix before its measurement can be taken.
- 2026-10-31 (interactive): measured with Measure-FactoryProcess.ps1 over a snapshot of Z:/repos/Curl.logs (files written since 2026-10-30 12:00; the one live run BL-4420-L1 left out as running) with a fresh 500-run CI list, at about 11:10 Arizona 2026-10-31. Since f54935070, filed CI fixes: BL-4359 filed 11:39, done 11:41; BL-4368 filed 14:02, done 14:07; BL-4378 claimed 03:02, done 03:12; but BL-4369 and BL-4370, filed 14:24 and 14:27 on 10-30 by the coordinator after its lanes had ended, were claimed only at 15:22 when the next shift started (58 min). None was requeued behind another task. Stays open: a CI fix filed with no lane running waits for the next shift.
- 2026-10-31 (interactive, 18:55 Arizona): measured after c3f18a294 (landed 11:04:39 Arizona). Window 11:04:39-18:35, every trace line in `Z:/repos/Curl.logs/DarkFactory-20261031-*.log` (coordinator and lane logs of shifts 104238 through 182927) and a fresh 500-run CI list. Red spells computed from that list by hand (the audit tool `Measure-FactoryProcess.ps1` is an audit path and was not run): 14:31-16:15 (104 min, runs 41239901600 and 41243817728, ExecuteAsync_NonAsciiPasswordOnPosix_IsEncodedAsUtf8), 17:08-17:26 (18 min, run 41252530857, flaky shard 3), 18:13-18:35 (22 min, run 41258993636). CI fixes filed: BL-4421 filed 12:14:22, claimed 12:16:27, requeued 12:50:43 ('cause not yet named', not behind another task), split 13:24 into BL-4427 (Blocked on a CI hang to read) and BL-4428; BL-4431 filed 15:07:14, claimed 15:07:55, Done 15:14:39; BL-4442 filed 17:56:33, claimed 18:03:14, the shift stopped from outside at 18:06, resumed 18:30:13, Done 18:33:44 (37 min); BL-4443 filed 17:56:33, a lane added for it 18:06:35 but stopped from outside with the shift, claimed 18:34:34 (38 min); BL-4444 filed 18:31:53, claimed 18:34:49. None was requeued behind another task (the AF-0502 cause holds fixed), but the expected result does not hold: the 104-min spell's failure ended at 14:31 during shift 132053's merge CI wait (14:06-15:06), and nothing filed BL-4431 until 15:07, because the CI watch ran only in the lane loop, which had ended. Fix ec555bb52: each merge-wait poll (`Invoke-MergeWaitPoll`, DarkFactory/ShiftCoordinator.ps1) runs the CI watch first, so such a failure is filed and starts the next shift at the next poll (about 5 min, not 36). -TestHeartbeat 32 PASS 0 FAIL under Windows PowerShell and pwsh. Back to Backlog to wait for a shift on ec555bb52 before the measurement is taken again.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-10-31: Doing -> Backlog. Fix ec555bb52 (CI watch runs during the merge's CI wait) waits for a shift before the measurement is taken again
- 2026-11-01: Backlog -> Doing. Interactive session: measure the window after ec555bb52 Criteria at claim: 87cd20bd 9742157d.
- 2026-11-01: Doing -> Done. Window after ec555bb52: 0 red spells, 0 CI fixes waiting; CI watch ran in the merge wait
