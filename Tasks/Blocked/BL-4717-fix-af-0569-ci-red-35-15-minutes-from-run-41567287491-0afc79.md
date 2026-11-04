---
id: BL-4717
title: Fix AF-0569: CI red 35.15 minutes from run 41567287491 (0afc799c), all of it after the fix 8fa866b82 had landed: 8 newer runs were cancelled before one finished, and two CI-fix tasks were filed after the fix
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
# BL-4717 — Fix AF-0569: CI red 35.15 minutes from run 41567287491 (0afc799c), all of it after the fix 8fa866b82 had landed: 8 newer runs were cancelled before one finished, and two CI-fix tasks were filed after the fix

## Goal

The cause of the factory behaviour the audit office reported as AF-0569 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0569 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0569-ci-red-35-15-minutes-from-run-41567287491-0afc799c.md`.

Location: `logs/DarkFactory-20261102-174304.log:74`

Location: `logs/DarkFactory-20261102-174304.log:74`

Spell 2026-11-03T03:06:46Z to 03:41:55Z, 35.15 minutes, from run 41567287491 on 0afc799c (logs/ci-runs.json:573). It failed on stale known-failure lines for RstStream_EndsTheCall_FailsWithTheStatusItsErrorCodeMapsTo and GoAway_LastStreamBeforeTheCall. Commit 8fa866b82 (BL-4594, 'drop known-failure lines the combined tree now passes') was made at 2026-11-02T19:46:28-07:00 (02:46:28Z), before the failing run ended. Runs 41568356821 (on 8fa866b8), 41568715899, 41569416558, 41569633497, 41570033273, 41570705387, 41570803068, 41570951688, 41571758205 and 41571869140 were all cancelled. 41570033273 was cancelled by the coordinator itself at 20:28:33 Arizona 'for lacking the CI fix BL-4604, BL-4605'. Green came only with 41572448811 at 03:41:55Z. The coordinator filed BL-4604 and BL-4605 at 19:55:19 Arizona, after the fix; L2 worked them one after the other (20:12:30, 20:23:01) and both closed with nothing to change ('already dropped by 8fa866b82'). This is the AF-0541 mechanism: red time after the fix, prolonged by cancelled runs.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-02 -LogRoot Z:\repos\Curl.audit\20261103-002553\logs -CiRunsJson Z:\repos\Curl.audit\20261103-002553\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 41567287491; git log -1 --format='%cI %s' 8fa866b82; Select-String -Path Z:\repos\Curl.audit\20261103-002553\logs\DarkFactory-20261102-174304*.log -Pattern 'BL-460[45]|41567287491|41570033273'
```

- Expected: No CI red spell over 30 minutes, and no red time after a fix has landed.
- Actual: Spell 2026-11-03T03:06:46Z to 03:41:55Z, 35.15 minutes, runId 41567287491; the fix 8fa866b82 was made at 2026-11-02T19:46:28-07:00; BL-4604 and BL-4605 were filed at 19:55:19 and closed with nothing to change.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause found: run 41567287491 (0afc799c) failed its Build and test jobs on all three platforms by 02:54Z, and the real fix 8fa866b82 had landed at 02:46Z. Run 41570033273 (9cefafda, started 03:05Z) held that fix and had no failed job, but the coordinator cancelled it at 03:28Z "for lacking the CI fix BL-4604, BL-4605" - tasks filed at 02:55Z, after the fix, that closed with nothing to change. `Stop-CiRunWithoutFix` counted any CI-fix task reaching Done as a fix the running run lacked, so green waited for run 41572448811 (03:41:55Z). The other cancelled runs were pending runs replaced by newer pushes, which is how ci.yml works on this branch.
- First fix: 28cbd2dfb (2026-11-03 10:51:22 Arizona) cancelled a running run for lacking a fix only once it had failed. Superseded by 5469c7711 (BL-4716), because it would have delayed AF-0568's cancel of run 41562433776, which had not failed yet.
- Fix: 5469c7711, pushed to work/dark-factory 2026-11-03 11:02:16 Arizona (18:02:16Z). `Stop-CiRunWithoutFix` counts a CI fix as one the running run lacks unless its "Doing -> Done." line names a commit the running commit already holds (`Test-CiFixAlreadyIn`, DarkFactory/CiWatch.ps1), as BL-4604 and BL-4605 did ("already dropped by 8fa866b82"), so run 41570033273 would not have been cancelled. The watch also looks every minute while CI is red (BL-4716). `-TestCiWatch`: 93/93 PASS under Windows PowerShell 5.1 and pwsh 7.
- Evidence still owed: the window starts at 2026-11-03T18:02:16Z and needs shift logs and CI runs after it, so the task waits in Backlog for an interactive session to run the measurement.
- Measurement (interactive session, 2026-11-03 16:33 MST): `powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since '2026-11-03 11:02:16' -LogRoot Z:\repos\Curl.logs -CiRunsJson <gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500, read 16:33 MST>`. Window 2026-11-03T18:02:16Z to 23:33Z. It holds shift DarkFactory-20261103-080214 (polling the board with no lanes until 13:46 MST), -134646 (13 lanes, 13:46-14:52, ended by a restart) and -145254 (13 lanes from 14:53; lanes ended 16:14, coordinator in its merge's CI wait at the read), and 54 CI runs created since 17:36:45Z: 7 finished success or failure, 46 pending runs cancelled by newer pushes, 1 running.
- Numbers: ciRedMinutes 71.8; ciRedSpells 41715208450 21:35:59Z-22:47:47Z 71.8 min, 41735630431 from 23:19:11Z still red; tasksDone 45, tasksClaimedMoreThanOnce 0, requeues 0, unfinishedRuns 0.
- What the spells were: no test failed in any of them before 23:12Z. 41688300433 (28cbd2df) and 41723085402 (441bcff2, which kept the second spell red) failed only because the `Audit paths untouched by the dark factory` job's `actions/checkout` (fetch-depth 0) ran past its 5-minute `timeout-minutes`; filed as BL-4782 (ci.yml, audit branch). 41715208450 (2533dfc6) failed because its macOS `Build and test` job "was not acquired by Runner of type hosted even after multiple attempts". The first spell lasted 2 hours because the next run, 41691198001 (ac11d86f), waited until 20:19Z for a runner for its audit-guard job, with no lane running. The coordinator traced all three runs as "cannot read its log with gh run view --log-failed" and never classed them: a CI watch bug (an empty log read as `$null`), fixed by BL-4783 (6f289fef1). Run 41735630431 (fad49f8f) failed IdleTimeout_ContainerAsks1000msAndWaits2500_CurlSendsEmp~ on Windows at 23:12:58Z and is still red; the watch read the failed job at 16:13:38 MST, 40 s later.
- Verdict: criterion 1 does not hold. The expected result is no red spell over 30 minutes, and the window holds one (71.8 min) and a second still red. It also cannot hold as written: the window runs from the fix's landing, so these spells stay in it. Moved to Blocked: an interactive session (or Stewart) has to decide whether red spells with no failing test (GitHub runner capacity, the audit-guard timeout BL-4782) count against the finding's expected result. If they do not, the window gets restated to start after BL-4782 lands, a criterion change made in Backlog. Otherwise the process auditor's re-audit judges the finding on the mechanism evidence below.
- AF-0569's mechanism (red time after a fix landed, a run that held the fix cancelled for lacking no-op CI fixes) did not recur. The coordinator cancelled no run in the window (no "cancelled run" trace), and no CI-fix task was filed for a failure an earlier commit had already fixed. AF-0569 itself was closed by the 2026-11-03_1017 re-audit ("no longer reproduces"). `-TestCiWatch` 96/96 under Windows PowerShell 5.1 and pwsh 7, including `Test-CiFixAlreadyIn` and "nothing cancelled (AF-0569)".
- Build and fast tests (criterion 2): `dotnet build` 0 errors; `dotnet test --filter "TestCategory!=Integration"` 115 projects, 54,924 passed, 0 failed, at 7410f102c (2026-11-03 16:12 MST).

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-11-03: Doing -> Backlog. Fix landed (28cbd2dfb at 2026-11-03 10:51 Arizona); the post-fix measurement needs shift logs and CI runs after it, so it waits in Backlog per its criterion.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-11-03: Doing -> Blocked. Criterion 1 cannot hold as written: the window from 2026-11-03T18:02:16Z holds CI red spells over 30 min (41715208450 21:35:59Z-22:47:47Z 71.8 min, 41735630431 from 23:19:11Z still red), from CI infrastructure (audit-guard timeout, BL-4782; macOS runner not acquired), not from the finding's mechanism, which did not recur. Needs an interactive session or Stewart to decide whether such spells count (restate the window after BL-4782 lands, in Backlog) or the process auditor's re-audit to judge.
