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
completed:
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

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the window's start: the fix's landing, or for a task with no fix yet the later of its fix's landing and ADR-1235's commit), gives the finding's expected result with CI red time counted only while a run on the branch has a failing test or build error (ADR-1235): a failed run whose only non-success jobs were never given a runner, ran into their `timeout-minutes` in a job that tests nothing, or were superseded by a newer commit is left out, as `gh run view <id> --json jobs` and each job's annotations classify it. Notes name the fix, its landing time, the window, each run left out and why, and the numbers. If no factory shift has run since the window's start, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: 2ab4542b 9742157d.
