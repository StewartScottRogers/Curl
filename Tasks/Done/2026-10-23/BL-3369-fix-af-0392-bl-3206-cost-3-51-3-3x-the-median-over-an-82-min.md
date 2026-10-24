---
id: BL-3369
title: Fix AF-0392: BL-3206 cost $3.51 (3.3x the median) over an 82-minute, 65-turn Opus run
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3369 — Fix AF-0392: BL-3206 cost $3.51 (3.3x the median) over an 82-minute, 65-turn Opus run

## Goal

What drove the past run's cost that the audit office reported as AF-0392 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0392 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0392-bl-3206-cost-3-51-3-3x-the-median-over-an-82-minut.md`.

Location: `logs/BL-3206-20261022-192710-L8.jsonl`

Location: `logs/BL-3206-20261022-192710-L8.jsonl`

Main run: Opus, 65 turns, 82.1 min, $3.27 (22:49:16 to 00:11:23). The first test line appears only at 00:00:50, 71 minutes after the claim. Resolve run: 11 turns, $0.25. The task (block in curl_multi_poll/curl_multi_wait on a handle's timeout) holds the lane far longer than the 16.7 minute median. The long untested stretch points to extended reading and design before the first build.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261023-061815\logs -CiRunsJson Z:\repos\Curl.audit\20261023-061815\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-061815\logs\DarkFactory-20261022-192710-L8.log -Pattern 'BL-3206 (claim|test|end|resolve)'
```

- Expected: costUsd at most 3x the median ($3.19).
- Actual: tasks[BL-3206].costUsd = 3.5135, minutes 91.62

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [x] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes
- Cause (read from the run log, 65 tool calls): BL-3206 was a real busy-wait bug in two projects (Curl.Engine.UnitLibrary, Libcurl). The run read the engine and wait code (calls 3-11), wrote and re-wrote a loopback repro file-app (12-19) to confirm the 100,000-turn spin, then added two new test files, one on the system clock (about 1.3 s each), had one test hang that needed TaskStop (40-45), ran Measure-CodeQuality.ps1 and the whole-solution fast tests (48-50), and wrote an ADR and a CLAUDE.md fix. Its task file set pipeline: feature and four touches, so this is the expected shape of such a task.
- Verdict: unavoidable, no fix. The reading was targeted (grep and sed slices, no whole-file dumps), the repro is what the lane rules require, and the one avoidable cost (a hung test, one TaskStop) was caught by the run itself and fixed by a cap on the loop. Cost was $3.51 against the $3.93 cap; no lane rule or RunDarkFactory.ps1 change would have cut it without dropping the quality run or the repro. RunDarkFactory.ps1 unchanged.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
- 2026-10-23: Doing -> Done. Cause named: a genuine two-project busy-wait fix with repro, ADR and quality run; unavoidable, no change
