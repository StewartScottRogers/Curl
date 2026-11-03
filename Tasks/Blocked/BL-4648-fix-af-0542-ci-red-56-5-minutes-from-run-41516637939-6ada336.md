---
id: BL-4648
title: Fix AF-0542: CI red 56.5 minutes from run 41516637939 (6ada3366): MQTT 5 CONNECT and Frame_Malformed conformance tests
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
# BL-4648 — Fix AF-0542: CI red 56.5 minutes from run 41516637939 (6ada3366): MQTT 5 CONNECT and Frame_Malformed conformance tests

## Goal

The cause of the factory behaviour the audit office reported as AF-0542 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0542 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0542-ci-red-56-5-minutes-from-run-41516637939-6ada3366.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells: start 2026-11-02T20:34:49Z (13:34 Arizona), end 21:31:19Z (14:31), 56.5 min, runId 41516637939 on 6ada3366a. Failing test: Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst, from the MQTT 5 reference broker added in 6007992eb (12:41). The coordinator filed BL-4543 at 13:28:52 from the failed jobs of the run while it was still going (DarkFactory-20261102-095114.log:110). L6 fixed it and pushed at 13:51:53. Duplicate fix tasks were also filed for the same test: BL-4544 (renumbered from a second BL-4543, L5, done 13:53:27) and BL-4547 (L3 claimed it at 13:47:08 and requeued it at 13:53:58 to wait on BL-4543; L1 reclaimed it and closed it at 14:10:19 as 'fixes confirmed'), so three fix runs covered one test. A second test, Frame_Malformed_IsAnsweredWithConnectionClose501 (run 41519828305), was filed as BL-4548 at 13:47:51 and closed at 14:11:17 as 'Already fixed by e6e55c175'. Run 41521714156 (e6e55c17) failed with an unreadable log. Green came with run 41526034389 at 21:31Z, 40 minutes after the first fix landed.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-02 -LogRoot Z:\repos\Curl.audit\20261102-172448\logs -CiRunsJson Z:\repos\Curl.audit\20261102-172448\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261102-172448\logs\DarkFactory-20261102-095114*.log -Pattern 'BL-454[3478] (ci|claim|REQUEUE|DONE)'
```

- Expected: No red spell over 30 minutes; one fix task per failing test
- Actual: runId 41516637939, 56.5 minutes; BL-4543, BL-4544 and BL-4547 all claimed for the same MQTT 5 CONNECT test

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause found: BL-4543 was the CI watch's task for the MQTT 5 CONNECT test (filed 13:28:46); BL-4544 and BL-4547 were "Fix flaky CI test Mqtt5ConnectTests.Connect_..." tasks two lanes filed from their own runs (13:31:00 and 13:44:42, renumbered from a shared BL-4543). Nothing at claim time compared a CI fix with the CI fixes already live, so all three were claimed.
- Fix: 033395c38, pushed to work/dark-factory 2026-11-03 10:57:18 Arizona (17:57:18Z). A claim reads the test a CI-fix title names, by method name from either the watch's or a lane's form (`Get-CiFixTestName`), and when a live CI fix in Doing, or an older one in Backlog, names the same test (`Get-CiFixDuplicateOf`), it moves the task to Deferred as that one's duplicate and claims something else (DarkFactory/Claim.ps1). The 56.5 red minutes are addressed by BL-4647's 73fa18c5a and BL-4717's 28cbd2dfb (the running run is now seen while it reads `queued`, and cancelled for lacking a fix only once it has failed). `-TestClaimDuplicates` 16/16, `-TestTaskReadiness` and `-TestCiWatch` PASS under Windows PowerShell 5.1 and pwsh 7.
- Evidence still owed: the window starts at 2026-11-03T17:57:18Z and needs shift logs and CI runs after it, so the task waits in Backlog for an interactive session to run the measurement.
- Measurement (interactive session, 2026-11-03 16:33 MST): `powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since '2026-11-03 10:57:18' -LogRoot Z:\repos\Curl.logs -CiRunsJson <gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500, read 16:33 MST>`. Window 2026-11-03T17:57:18Z to 23:33Z. It holds shift DarkFactory-20261103-080214 (polling the board with no lanes until 13:46 MST), -134646 (13 lanes, 13:46-14:52, ended by a restart) and -145254 (13 lanes from 14:53; lanes ended 16:14, coordinator in its merge's CI wait at the read), and 54 CI runs created since 17:36:45Z: 7 finished success or failure, 46 pending runs cancelled by newer pushes, 1 running.
- Numbers: ciRedMinutes 71.8; ciRedSpells 41715208450 21:35:59Z-22:47:47Z 71.8 min, 41735630431 from 23:19:11Z still red; tasksDone 45, tasksClaimedMoreThanOnce 0, requeues 0, unfinishedRuns 0.
- What the spells were: no test failed in any of them before 23:12Z. 41688300433 (28cbd2df) and 41723085402 (441bcff2, which kept the second spell red) failed only because the `Audit paths untouched by the dark factory` job's `actions/checkout` (fetch-depth 0) ran past its 5-minute `timeout-minutes`; filed as BL-4782 (ci.yml, audit branch). 41715208450 (2533dfc6) failed because its macOS `Build and test` job "was not acquired by Runner of type hosted even after multiple attempts". The first spell lasted 2 hours because the next run, 41691198001 (ac11d86f), waited until 20:19Z for a runner for its audit-guard job, with no lane running. The coordinator traced all three runs as "cannot read its log with gh run view --log-failed" and never classed them: a CI watch bug (an empty log read as `$null`), fixed by BL-4781 (93ceba3a9). Run 41735630431 (fad49f8f) failed IdleTimeout_ContainerAsks1000msAndWaits2500_CurlSendsEmp~ on Windows at 23:12:58Z and is still red; the watch read the failed job at 16:13:38 MST, 40 s later.
- Verdict: criterion 1 does not hold. The expected result is no red spell over 30 minutes, and the window holds one (71.8 min) and a second still red. It also cannot hold as written: the window runs from the fix's landing, so these spells stay in it. Moved to Blocked: an interactive session (or Stewart) has to decide whether red spells with no failing test (GitHub runner capacity, the audit-guard timeout BL-4782) count against the finding's expected result. If they do not, the window gets restated to start after BL-4782 lands, a criterion change made in Backlog. Otherwise the process auditor's re-audit judges the finding on the mechanism evidence below.
- AF-0542's mechanism (several fix tasks for one test) did not recur. One CI-fix task was filed in the window: BL-4780, for MissedPing_EndsTheConnectionAfterPingIntervalPlusPingTimeoutOnTheInjectedClock. It was claimed once, by L1 at 15:15:13 MST, and no claim deferred a duplicate. That is one fix task per failing test. tasksClaimedMoreThanOnce 0. `-TestClaimDuplicates` 16/16 under Windows PowerShell 5.1 and pwsh 7.
- Build and fast tests (criterion 2): `dotnet build` 0 errors; `dotnet test --filter "TestCategory!=Integration"` 115 projects, 54,924 passed, 0 failed, at 7410f102c (2026-11-03 16:12 MST).

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-11-03: Doing -> Backlog. Fix landed (033395c38 at 2026-11-03 10:57 Arizona); the post-fix measurement needs shift logs and CI runs after it, so it waits in Backlog per its criterion.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-11-03: Doing -> Blocked. Criterion 1 cannot hold as written: the window from 2026-11-03T17:57:18Z holds CI red spells over 30 min (41715208450 21:35:59Z-22:47:47Z 71.8 min, 41735630431 from 23:19:11Z still red), from CI infrastructure (audit-guard timeout, BL-4782; macOS runner not acquired), not from the finding's mechanism, which did not recur. Needs an interactive session or Stewart to decide whether such spells count (restate the window after BL-4782 lands, in Backlog) or the process auditor's re-audit to judge.
