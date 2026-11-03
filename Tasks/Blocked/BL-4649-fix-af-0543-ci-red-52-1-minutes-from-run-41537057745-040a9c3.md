---
id: BL-4649
title: Fix AF-0543: CI red 52.1 minutes from run 41537057745 (040a9c35): Windows quality gate failed on AMQP 1.0 container members a lane integrated over the gates
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
# BL-4649 — Fix AF-0543: CI red 52.1 minutes from run 41537057745 (040a9c35): Windows quality gate failed on AMQP 1.0 container members a lane integrated over the gates

## Goal

The cause of the factory behaviour the audit office reported as AF-0543 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0543 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0543-ci-red-52-1-minutes-from-run-41537057745-040a9c35.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells: start 2026-11-02T22:59:50Z (15:59 Arizona), end 23:51:56Z (16:51), 52.1 min, runId 41537057745 on 040a9c352. Failure: job 'Quality gates (windows-latest) step Measure coverage, complexity and CRAP' (DarkFactory-20261102-095114.log:151, 16:02:02). The members outside the gates were Amqp1ReferenceContainer.Accepts and AnswerAsync, from 9598845a7 (14:28, BL-4545). L7's quality checks on that task reported 'ok' (DarkFactory-20261102-095114-L7.log, 14:22:43 to 14:25:19), while the split of those very members was left to BL-4546, and that split's parent BL-4527 had been requeued at 12:52 with '9 Amqp1 members are still over the complexity or branch gate'. BL-4578 was filed at 16:10:52 and claimed by L5 at 16:18:14, then requeued at 16:21:45 to wait on BL-4546 (pushed 16:21:14). L4 reclaimed it at 16:22:27 and closed it at 16:33:26. A second failure, Grpcs_ServerSelectsH2_OffersAlpnH2AndMakesTheCallOverTls (run 41541574110), was filed as BL-4579 at 16:15:42 and closed at 16:24:11 as already handled. Green came with run 41546343806 at 23:51:56Z.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-02 -LogRoot Z:\repos\Curl.audit\20261102-172448\logs -CiRunsJson Z:\repos\Curl.audit\20261102-172448\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261102-172448\logs\DarkFactory-20261102-095114.log -Pattern '41537057745|BL-4578'
```

- Expected: No red spell over 30 minutes
- Actual: runId 41537057745, start 2026-11-02T22:59:50Z, end 2026-11-02T23:51:56Z, minutes 52.1; Quality gates (windows-latest) failure

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause found: lane 7's run of BL-4545 did run Measure-CodeQuality.ps1 for Curl.Conformance.Extended.UnitLibrary (BL-4545-20261102-095114-L7.jsonl: four runs exited 1). It judged the two members still outside a gate, `Amqp1ReferenceContainer.Accepts` and `AnswerAsync`, as BL-4546's and committed them anyway. The trace's "quality ok" only meant the shell command ran, since the run appended `echo exit $?`. Nothing in the factory measured the gates before the push.
- Fix: ac11d86f4, pushed to work/dark-factory 2026-11-03 11:09:35 Arizona (18:09:35Z). Before taking the integrate lock, a lane runs Measure-CodeQuality.ps1 for the gated libraries whose C# files its commits change (`Test-QualityGates`, DarkFactory/Integrate.ps1). If a member of a changed file is outside a gate, a finished task gets one headless repair run (`Repair-QualityGates`, `$QualityRepairPrompt` in DarkFactory/Prompts.ps1); otherwise the work does not integrate and is parked. Members of files the lane left alone do not count. RunDarkFactory.ps1's help says so. Red time after a fix is also cut by BL-4647's 73fa18c5a and BL-4716's 5469c7711. `-TestIntegrateLock` 23/23 PASS under Windows PowerShell 5.1 and pwsh 7.
- Evidence still owed: the window starts at 2026-11-03T18:09:35Z and needs shift logs and CI runs after it, so the task waits in Backlog for an interactive session to run the measurement.
- Measurement (interactive session, 2026-11-03 16:33 MST): `powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since '2026-11-03 11:09:35' -LogRoot Z:\repos\Curl.logs -CiRunsJson <gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500, read 16:33 MST>`. Window 2026-11-03T18:09:35Z to 23:33Z. It holds shift DarkFactory-20261103-080214 (polling the board with no lanes until 13:46 MST), -134646 (13 lanes, 13:46-14:52, ended by a restart) and -145254 (13 lanes from 14:53; lanes ended 16:14, coordinator in its merge's CI wait at the read), and 54 CI runs created since 17:36:45Z: 7 finished success or failure, 46 pending runs cancelled by newer pushes, 1 running.
- Numbers: ciRedMinutes 71.8; ciRedSpells 41715208450 21:35:59Z-22:47:47Z 71.8 min, 41735630431 from 23:19:11Z still red; tasksDone 45, tasksClaimedMoreThanOnce 0, requeues 0, unfinishedRuns 0.
- What the spells were: no test failed in any of them before 23:12Z. 41688300433 (28cbd2df) and 41723085402 (441bcff2, which kept the second spell red) failed only because the `Audit paths untouched by the dark factory` job's `actions/checkout` (fetch-depth 0) ran past its 5-minute `timeout-minutes`; filed as BL-4782 (ci.yml, audit branch). 41715208450 (2533dfc6) failed because its macOS `Build and test` job "was not acquired by Runner of type hosted even after multiple attempts". The first spell lasted 2 hours because the next run, 41691198001 (ac11d86f), waited until 20:19Z for a runner for its audit-guard job, with no lane running. The coordinator traced all three runs as "cannot read its log with gh run view --log-failed" and never classed them: a CI watch bug (an empty log read as `$null`), fixed by BL-4781 (93ceba3a9). Run 41735630431 (fad49f8f) failed IdleTimeout_ContainerAsks1000msAndWaits2500_CurlSendsEmp~ on Windows at 23:12:58Z and is still red; the watch read the failed job at 16:13:38 MST, 40 s later.
- Verdict: criterion 1 does not hold. The expected result is no red spell over 30 minutes, and the window holds one (71.8 min) and a second still red. It also cannot hold as written: the window runs from the fix's landing, so these spells stay in it. Moved to Blocked: an interactive session (or Stewart) has to decide whether red spells with no failing test (GitHub runner capacity, the audit-guard timeout BL-4782) count against the finding's expected result. If they do not, the window gets restated to start after BL-4782 lands, a criterion change made in Backlog. Otherwise the process auditor's re-audit judges the finding on the mechanism evidence below.
- AF-0543's mechanism (a lane integrating members outside a quality gate) did not recur. All 28 `Quality gates` jobs of the 7 finished CI runs in the window passed (gh run view --json jobs). Lanes measured the gates before integrating 4 times, and each time the trace reads "quality gates met in the files it changed": BL-4775 L11 14:45, BL-4774 L13 15:10, BL-4773 L8 15:29 and BL-4771 L5 15:40 MST. `-TestIntegrateLock` 24/24 under Windows PowerShell 5.1 and pwsh 7.
- Build and fast tests (criterion 2): `dotnet build` 0 errors; `dotnet test --filter "TestCategory!=Integration"` 115 projects, 54,924 passed, 0 failed, at 7410f102c (2026-11-03 16:12 MST).

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-11-03: Doing -> Backlog. Fix landed (ac11d86f4 at 2026-11-03 11:09 Arizona); the post-fix measurement needs shift logs and CI runs after it, so it waits in Backlog per its criterion.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-11-03: Doing -> Blocked. Criterion 1 cannot hold as written: the window from 2026-11-03T18:09:35Z holds CI red spells over 30 min (41715208450 21:35:59Z-22:47:47Z 71.8 min, 41735630431 from 23:19:11Z still red), from CI infrastructure (audit-guard timeout, BL-4782; macOS runner not acquired), not from the finding's mechanism, which did not recur. Needs an interactive session or Stewart to decide whether such spells count (restate the window after BL-4782 lands, in Backlog) or the process auditor's re-audit to judge.
