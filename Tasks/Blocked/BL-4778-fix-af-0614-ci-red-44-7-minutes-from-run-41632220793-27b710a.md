---
id: BL-4778
title: Fix AF-0614: CI red 44.7 minutes from run 41632220793 (27b710a0): stale ProtoJSON known-failure lines whose fix run was cancelled, then a DoQ regression
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-11-03
completed:
---
# BL-4778 — Fix AF-0614: CI red 44.7 minutes from run 41632220793 (27b710a0): stale ProtoJSON known-failure lines whose fix run was cancelled, then a DoQ regression

## Goal

The cause of the factory behaviour the audit office reported as AF-0614 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0614 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0614-ci-red-44-7-minutes-from-run-41632220793-27b710a0.md`.

Location: `logs/DarkFactory-20261103-022445.log:41`

Location: `logs/DarkFactory-20261103-022445.log:41`

ciRedSpells: start 2026-11-03T12:19:28Z, end 13:04:10Z, 44.7 minutes, runId 41632220793. That run on 27b710a0 failed RoundTrip_WrapperTypes_AreTheWrappedValuesAndKeepAZero, RoundTrip_StructValueAndListValue_AreAnyJsonObjectValueAndArray and RoundTrip_FieldMask_IsOneStringOfLowerCamelCasePaths: known-failure lines kept by a merge that the tree now passes. The fix 1df901115 (05:11 MST) was already on the branch, but its run 41633887707 was cancelled at 12:16:26Z by a newer push. BL-4724/4725/4726 were filed at 05:20:28 MST, 1 minute after the run ended, and closed 05:31-05:34 MST: BL-4724 'Already fixed by 1df901115', BL-4725 'already fixed by 1df901115', BL-4726 'its stale Know~'. Run 41634593982 on 62178b13 (finished 12:31:32Z) then failed Quic_DefaultPort853_SendsTheLengthPrefixedQueryWithMessageId0AndFinOnTheFi~ and Quic_Query_IsPaddedWithTheEdnsPaddingOption. Filing it failed at 05:34:51 ('could not push the CI failure tasks'), so BL-4727/BL-4728 were filed at 05:36:08 MST and fixed by 05:47:09 MST (bd78278df). Green at 13:04:10Z (run 41639250979 on eae28957), 17 minutes after the last fix. The coordinator cancelled 2 runs that lacked the fixes.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-03 -LogRoot Z:\repos\Curl.audit\20261103-101701\logs -CiRunsJson Z:\repos\Curl.audit\20261103-101701\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 41632220793; Select-String -Path Z:\repos\Curl.audit\20261103-101701\logs\DarkFactory-20261103-022445*.log -Pattern 'BL-472[45678] (ci|claim|DONE)|41632220793|41634593982|could not push'
```

- Expected: No red spell over 30 minutes
- Actual: Spell of 44.7 minutes from 41632220793; 3 tasks filed for failures fixed by 1df901115, whose run was cancelled; DoQ regression filed after a failed push

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes
- Measurement (interactive session, 2026-11-03 16:33 MST): `powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since '2026-11-03 11:02:16' -LogRoot Z:\repos\Curl.logs -CiRunsJson <gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500, read 16:33 MST>`. Window 2026-11-03T18:02:16Z to 23:33Z. It holds shift DarkFactory-20261103-080214 (polling the board with no lanes until 13:46 MST), -134646 (13 lanes, 13:46-14:52, ended by a restart) and -145254 (13 lanes from 14:53; lanes ended 16:14, coordinator in its merge's CI wait at the read), and 54 CI runs created since 17:36:45Z: 7 finished success or failure, 46 pending runs cancelled by newer pushes, 1 running.
- Numbers: ciRedMinutes 71.8; ciRedSpells 41715208450 21:35:59Z-22:47:47Z 71.8 min, 41735630431 from 23:19:11Z still red; tasksDone 45, tasksClaimedMoreThanOnce 0, requeues 0, unfinishedRuns 0.
- What the spells were: no test failed in any of them before 23:12Z. 41688300433 (28cbd2df) and 41723085402 (441bcff2, which kept the second spell red) failed only because the `Audit paths untouched by the dark factory` job's `actions/checkout` (fetch-depth 0) ran past its 5-minute `timeout-minutes`; filed as BL-4782 (ci.yml, audit branch). 41715208450 (2533dfc6) failed because its macOS `Build and test` job "was not acquired by Runner of type hosted even after multiple attempts". The first spell lasted 2 hours because the next run, 41691198001 (ac11d86f), waited until 20:19Z for a runner for its audit-guard job, with no lane running. The coordinator traced all three runs as "cannot read its log with gh run view --log-failed" and never classed them: a CI watch bug (an empty log read as `$null`), fixed by BL-4781 (93ceba3a9). Run 41735630431 (fad49f8f) failed IdleTimeout_ContainerAsks1000msAndWaits2500_CurlSendsEmp~ on Windows at 23:12:58Z and is still red; the watch read the failed job at 16:13:38 MST, 40 s later.
- Verdict: criterion 1 does not hold. The expected result is no red spell over 30 minutes, and the window holds one (71.8 min) and a second still red. It also cannot hold as written: the window runs from the fix's landing, so these spells stay in it. Moved to Blocked: an interactive session (or Stewart) has to decide whether red spells with no failing test (GitHub runner capacity, the audit-guard timeout BL-4782) count against the finding's expected result. If they do not, the window gets restated to start after BL-4782 lands, a criterion change made in Backlog. Otherwise the process auditor's re-audit judges the finding on the mechanism evidence below.
- Cause found: the 44.7 minutes were two failures in a row. (1) Run 41632220793 (27b710a0) failed on three ProtoJSON known-failure lines that 1df901115 (BL-4691, 05:11 MST) had already dropped. That fix's own run 41633887707 was a pending run replaced by a newer push, which is how ci.yml works on this branch, and the next run held the fix. BL-4724/4725/4726 were filed after the fix because the watch counts a failure as covered only by a task that names the test, and BL-4691 named none. They cost three short lane runs, not red time. (2) Run 41634593982 (62178b13), which held the fix, failed on a new DoQ regression that lanes, testing only on Windows, could not see. Its failed Linux job went unread while the run reported `queued`. Filing it waited a heartbeat after a failed push ("could not push the CI failure tasks", 05:34:51), and green came 17 minutes after the fix bd78278df.
- Fixes: no new code was needed for AF-0614's red time. The fixes already pushed cover it: 73fa18c5a (BL-4647, 10:47 MST) reads and cancels the running run while it reports `queued`; 5469c7711 (BL-4716/BL-4717, 11:02 MST, 18:02:16Z) makes the watch look every minute while CI is red, so a failed filing push is retried within a minute, and keeps the coordinator from cancelling a run that holds a fix. The window starts at the later one. `-TestCiWatch` 96/96 under Windows PowerShell 5.1 and pwsh 7, including "a failed job of a holder that reads queued is read now" and "the watch looks every minute while CI is red". This measurement turned up a further CI watch bug, fixed separately as BL-4781 (93ceba3a9).
- AF-0614's mechanism did not recur in the window: no CI-fix task was filed for an already-fixed failure, no failed filing push was traced, and no run was cancelled.
- Build and fast tests (criterion 2): `dotnet build` 0 errors; `dotnet test --filter "TestCategory!=Integration"` 115 projects, 54,924 passed, 0 failed, at 7410f102c (2026-11-03 16:12 MST).

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-11-03: Doing -> Blocked. Criterion 1 cannot hold as written: the window from 2026-11-03T18:02:16Z holds CI red spells over 30 min (41715208450 21:35:59Z-22:47:47Z 71.8 min, 41735630431 from 23:19:11Z still red), from CI infrastructure (audit-guard timeout, BL-4782; macOS runner not acquired), not from the finding's mechanism, which did not recur. Needs an interactive session or Stewart to decide whether such spells count (restate the window after BL-4782 lands, in Backlog) or the process auditor's re-audit to judge.
