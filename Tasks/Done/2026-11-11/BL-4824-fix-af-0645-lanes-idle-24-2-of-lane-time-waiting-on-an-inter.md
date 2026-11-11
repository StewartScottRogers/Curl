---
id: BL-4824
title: Fix AF-0645: Lanes idle 24.2% of lane time waiting on an interactive-only Backlog
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-11-04
completed: 2026-11-06
---
# BL-4824 — Fix AF-0645: Lanes idle 24.2% of lane time waiting on an interactive-only Backlog

## Goal

The cause of the factory behaviour the audit office reported as AF-0645 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0645 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0645-lanes-idle-24-2-of-lane-time-waiting-on-an-interac.md`.

Location: `logs/DarkFactory-20261103-185800-L1.log:2`

Location: `logs/DarkFactory-20261103-185800-L1.log:2`

waitNothingReadyMinutes=108 of laneMinutes=445.55 (24.2%, over the 20% bar); waitOverlapMinutes=0. The 185800 shift (a restart of 183829) waited from 18:58 to 19:17 on lanes L1-L7, then L8 ended 'nothing a lane can claim'. Each wait line reads 'No task is ready. Backlog is interactive only, waiting on Stewart or an interactive session'. The same message recurs in 195556 L1/L2 from 20:00 to 20:11, and the 165213 lanes waited from 16:57 to 17:02. The whole Backlog at the audited commit is interactive only. BL-4782 touches the guard file .github/workflows/ci.yml. BL-4797, BL-4798 and BL-4799 (fixes for process findings AF-0624 to AF-0626, touches RunDarkFactory.ps1) carry 'lane: no', which New-TasksFromAcceptedFindings.ps1 sets for every process finding's task (self-test AF-0014). Seven more CI-red fix tasks (BL-4647, BL-4648, BL-4649, BL-4715, BL-4716, BL-4717, BL-4778) were Blocked at 22:03 ('Criterion 1 cannot hold as written'). The coordinator reported 'Audit due: ... interactive-stuck:3' seven times (16:15 to 21:47) without any interactive session taking these tasks. So 8 restarted lanes were spun up into a queue they could not claim from.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-03 -LogRoot Z:\repos\Curl.audit\20261103-220320\logs -CiRunsJson Z:\repos\Curl.audit\20261103-220320\logs\ci-runs.json -OutFile $env:TEMP\process.json; $j = Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json; $j.waitNothingReadyMinutes / $j.laneMinutes; Select-String -Path Z:\repos\Curl.audit\20261103-220320\logs\DarkFactory-20261103-*-L*.log -Pattern 'interactive only' | Measure-Object
```

- Expected: waitNothingReadyMinutes at most 20% of laneMinutes
- Actual: 108 / 445.55 = 0.242; 46 'interactive only' wait lines

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the window's start: the fix's landing, or for a task with no fix yet the later of its fix's landing and ADR-1235's commit), gives the finding's expected result with CI red time counted only while a run on the branch has a failing test or build error (ADR-1235): a failed run whose only non-success jobs were never given a runner, ran into their `timeout-minutes` in a job that tests nothing, or were superseded by a newer commit is left out, as `gh run view <id> --json jobs` and each job's annotations classify it. Notes name the fix, its landing time, the window, each run left out and why, and the numbers. If no factory shift has run since the window's start, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Measured 2026-11-06 (interactive) over shift 20261106-051223 (05:12-13:12, after BL-4846's capacity rerun landed): Measure-FactoryProcess.ps1 -Since 2026-11-06 with a fresh CI run list gives laneMinutes 810.65, waitNothingReadyMinutes 0, waitOverlapMinutes 70 (8.6%), 17 tasks done, one CI red spell of 94.52 min (run 42239087175, e9ba9daf: SocketAction_TimeoutAgainstASilentServer_TellsTheTimeout... failing on Linux, filed as BL-4897 at 08:20, ended by run 42249533661's success at 09:56 after the watch's capacity rerun at 09:34), no infrastructure-only runs. No code changed by this task; CI passed build and fast tests on all three platforms at 6065a1560.

- Cause found: with nothing ready, a lane waited whenever any lane-eligible task was in Doing, since its end might make more ready (DarkFactory/Claim.ps1, `Invoke-Claim`). In restarted shift 20261103-185800 six lanes resumed adopted tasks (BL-4790 to BL-4796) that no task depended on, and the whole Backlog was interactive only, so lanes 1 and 5 waited from 18:58 and each lane that finished its task waited too, until the 19:15:54 lane step retired them ("lanes 8 -> 1"). The wait lines name the interactive-only Backlog, but what kept the lanes waiting was the lane-eligible work in Doing. A lane already ends rather than waits when only interactive-only work is left in Doing (AF-0435).
- Fix: aef2fa6d2, pushed to work/dark-factory 2026-11-04 05:24:14 MST (12:24:14Z). A lane with nothing ready waits only when a lane-eligible Backlog task's `depends-on` names a task in Doing, or a lane-eligible Blocked task's last Log line names one (Invoke-Requeue moves that one back once it is Done) - `Test-DoingMayMakeReady`; otherwise it ends ("nothing ready"), and the lanes still running claim any new work, a CI fix gets a lane of its own (ADR-1158), and a -Continuous shift's idle poll starts the next shift once something is claimable. Settling the twelve interactive-only process tasks (BL-4647 to BL-4824) on 2026-11-04 also empties most of the interactive-only Backlog the finding names. `-TestTaskReadiness` 33/33 under Windows PowerShell 5.1 and pwsh 7, with the new cases "a lane-eligible task in Doing that no lane task waits on ends the lane rather than waits (AF-0645)", "a Blocked task whose last Log line names a task in Doing may be made ready by it" and "a lane-eligible task in Doing that a Backlog task depends on keeps the lane waiting"; `-TestClaimDuplicates` 16/16.
- Window: from the fix, 2026-11-04T12:24:14Z (ADR-1235's commit b6dd48c99 landed in the same push). No factory shift has run since, so criterion 1 waits in Backlog. For reference, before the fix (gh run list read 2026-11-04 05:05 MST): from 2026-11-03 10:44:04 MST, waitNothingReadyMinutes 123 of laneMinutes 2015.87 (6.1%); from 2026-11-03 16:32:49 MST, 113 of 826.13 (13.7%), both under the 20% bar.
- Build and fast tests (criterion 2), 2026-11-04 at aef2fa6d2, which holds the fix: `dotnet build` 0 warnings, 0 errors; `dotnet test --filter "TestCategory!=Integration"` exit 0, no project failed.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: 2ab4542b 9742157d.
- 2026-11-04: Doing -> Backlog. Fix landed 2026-11-04 05:24:14 MST (aef2fa6d2, a lane ends when nothing in Doing can make a task ready); the window starts there and no factory shift has run since, so criterion 1 waits in Backlog for an interactive session to measure it.
- 2026-11-06: Backlog -> Doing. Criteria at claim: 2ab4542b 9742157d.
- 2026-11-06: Doing -> Done. Measured over shift 20261106-051223: overlap wait 8.6%, nothing-ready wait 0% of 810.65 lane minutes, both under 20%.
