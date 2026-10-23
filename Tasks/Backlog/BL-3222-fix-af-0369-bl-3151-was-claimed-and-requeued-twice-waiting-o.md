---
id: BL-3222
title: Fix AF-0369: BL-3151 was claimed and requeued twice waiting on a shift-end merge it could not cause; the second claim came from a shift started for it during that merge's CI wait
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-23
completed:
---
# BL-3222 — Fix AF-0369: BL-3151 was claimed and requeued twice waiting on a shift-end merge it could not cause; the second claim came from a shift started for it during that merge's CI wait

## Goal

The cause of the factory behaviour the audit office reported as AF-0369 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0369 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0369-bl-3151-was-claimed-and-requeued-twice-waiting-on.md`.

Location: `logs/DarkFactory-20261022-172108-L1.log`

Location: `logs/DarkFactory-20261022-172108-L1.log`

Measure-FactoryProcess: BL-3151 claims=2, outcome=requeued, costUsd=0.9619, minutes=1.35. Task Log: 'Doing -> Backlog. Waiting for the shift-end merge to put BL-3147 (fb690a3b1) on master...' and then 'Still waiting for the shift-end merge... Set lane: no until that holds'. Claim 1: L1 at 17:08:48 (shift 164804), 26 turns, $0.643, REQUEUE 17:11:59. Then DarkFactory-20261022-164804.log at 17:21:07 says 'BL-3151 is claimable during the merge's CI wait; next shift started'. Claim 2: L1 at 17:21:30 (shift 172108), 12 turns, $0.319, REQUEUE 17:22:51. The merge it waited for came at 17:42:15 ('merged 2f68618 into master (pull request #209)'). So the coordinator started a shift to claim a task whose only blocker was the merge it was still waiting on. Nothing to finish at either claim. Waste: 2 Opus runs, $0.96, no task done. Under 20% of lane time, so Low.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-190247\logs -CiRunsJson Z:\repos\Curl.audit\20261022-190247\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261022-190247\logs\DarkFactory-20261022-1*.log -Pattern 'BL-3151 (claim|REQUEUE|is claimable)'
```

- Expected: No task requeued twice. A task waiting on the shift-end merge is not claimed again before that merge lands.
- Actual: BL-3151 claims=2, outcome=requeued. 2 claims (17:08:48, 17:21:30), 2 REQUEUE lines, and '17:21:07 BL-3151 is claimable during the merge's CI wait; next shift started' before 'merged 2f68618 into master' at 17:42:15.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Fix (2026-10-23): the poll during the shift-end merge's CI wait (BL-3142) now calls `Get-MergeWaitClaimableTaskId` (DarkFactory/ShiftCoordinator.ps1), which skips every task in the shift's `requeued.txt` (`Get-ShiftRequeued`). Lanes already record a requeue there, so a task that waited on this merge is not offered to the shift started beside it; the next shift tries it afresh. Tests: three cases in the -TestHeartbeat rehearsal (DarkFactory/LaneState.ps1); -TestHeartbeat and -TestTaskIds pass with no FAIL. Only PowerShell changed, so dotnet build and tests were not rerun. Live-log measurement not done: no shift has run since the fix, so criterion 1 stays open and the task waits in Backlog.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Interactive session claims AF-0369 fix Criteria at claim: 87cd20bd 9742157d.
- 2026-10-23: Doing -> Backlog. Fix landed; live-log measurement needs a shift run after it
