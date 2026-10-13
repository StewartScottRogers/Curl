---
id: BL-2148
title: Fix AF-0189: Lanes idle 82 of 272 lane minutes (30.1%) with nothing ready while the whole BL-2127/BL-2126/BL-2128 chain waited on interactive-only BL-2125
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-13
completed:
---
# BL-2148 — Fix AF-0189: Lanes idle 82 of 272 lane minutes (30.1%) with nothing ready while the whole BL-2127/BL-2126/BL-2128 chain waited on interactive-only BL-2125

## Goal

The defect the audit office reported as AF-0189 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0189 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0189-lanes-idle-82-of-272-lane-minutes-30-1-with-nothin.md`.

Location: `logs/DarkFactory-20261013-061911-L1.log:57`

Location: `logs/DarkFactory-20261013-061911-L1.log:57`

Measure-FactoryProcess.ps1 -Since 2026-10-12: waitNothingReadyMinutes 82, laneMinutes 272.02 (30.1%, over the 20% bar); waitOverlapMinutes 46. Shift 20261013-061911 started 3 lanes. After BL-2131's DONE at 06:52:23, lane 1 logged 'No task is ready.' 24 times from 06:52:27 to 07:16:04. Lanes 2 and 3 logged it from 06:39:24/06:39:30 until they retired at 07:06 ('lanes 3 -> 1 (1 ready task can run at once)'). That is about 78 of the 82 minutes. Board at 7849345b1 (06:50): Backlog held BL-2125 (lane: no, interactive only), BL-2127 (depends-on [BL-2125]), BL-2126 (depends-on [BL-2127]), BL-2128 (lane: no, depends-on [BL-2125, BL-2126, BL-2127]) and BL-2132 (lane: no); Doing held BL-2108 (lane: no, interactive). Every Backlog task was interactive only or waited on one. BL-2125 finished interactively at 07:31 (df6e2af57), and BL-2127 was claimed by a lane only at 09:22:21 in the next shift. Shift 215436 (22:04-22:08) and shift 092151 lane 2 (09:22-09:24, Backlog: BL-2126 behind BL-2127, BL-2128 lane: no) added the rest.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-12 -LogRoot Z:\repos\Curl.audit\20261013-092506\logs -CiRunsJson Z:\repos\Curl.audit\20261013-092506\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261013-092506\logs\DarkFactory-20261013-061911-L*.log -Pattern 'No task is ready' | Measure-Object; git show 7849345b1:Tasks/Backlog/BL-2127-curl-reports-itself-as-curl-8-22-0-in-v-the-user-agent-write.md | Select-String 'depends-on'
```

- Expected: waitNothingReadyMinutes at most 20% of laneMinutes, or no lane-eligible chain held behind a lane: no task
- Actual: waitNothingReadyMinutes 82 of laneMinutes 272.02 (30.1%); 61 'No task is ready' lines in shift 061911; BL-2127 depends-on: [BL-2125], and BL-2125 is lane: no

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-13: Created.
