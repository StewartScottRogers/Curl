---
id: BL-4715
title: Fix AF-0567: CI red 54.97 minutes from run 41549222390 (6dfcd0aa): a stale known-failure line for Flow_FromTheBroker_IsAnsweredWithFlowOk, filed 32 minutes late because shift starts were refused over orphaned BL-2969
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
# BL-4715 — Fix AF-0567: CI red 54.97 minutes from run 41549222390 (6dfcd0aa): a stale known-failure line for Flow_FromTheBroker_IsAnsweredWithFlowOk, filed 32 minutes late because shift starts were refused over orphaned BL-2969

## Goal

The cause of the factory behaviour the audit office reported as AF-0567 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0567 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0567-ci-red-54-97-minutes-from-run-41549222390-6dfcd0aa.md`.

Location: `logs/DarkFactory-20261102-174304.log:31`

Location: `logs/DarkFactory-20261102-174304.log:31`

Spell 2026-11-03T00:22:12Z to 01:17:10Z, 54.97 minutes, from run 41549222390 on 6dfcd0aa (logs/ci-runs.json:797). Shift starts at 17:25:01 and 17:40:37 Arizona were refused: 'BL-2969 is in Doing and held by no lane' (DarkFactory-20261102-172455.log:1, DarkFactory-20261102-174031.log:1). CI watch first ran at 17:54:19 and filed BL-4584, 32 minutes after the run failed. L2 claimed it at 17:57:20 and finished in 2.5 minutes with nothing to change: 'its stale known-failure line was removed in 844d53972'. That commit (BL-4563, 00:56:31Z) was the real fix; it was integrated by L2 at 17:57:01 when it pushed the stopped lane's BL-4563 work. Runs 41556437882, 41556616280 and 41556972129 after it were cancelled; 41557110247 went green at 01:17:10Z. Cause: a lane integrated a fix that made an Extended conformance case pass without dropping its KnownFailures line.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-02 -LogRoot Z:\repos\Curl.audit\20261103-002553\logs -CiRunsJson Z:\repos\Curl.audit\20261103-002553\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 41549222390; Select-String -Path Z:\repos\Curl.audit\20261103-002553\logs\DarkFactory-20261102-1*.log -Pattern 'BL-4584|41549222390|refuse'
```

- Expected: No CI red spell over 30 minutes.
- Actual: Spell 2026-11-03T00:22:12Z to 01:17:10Z, 54.97 minutes, runId 41549222390; refused shift starts at 17:25:01 and 17:40:37; BL-4584 filed 17:54:19.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the window's start: the fix's landing, or for a task with no fix yet the later of its fix's landing and ADR-1235's commit), gives the finding's expected result with CI red time counted only while a run on the branch has a failing test or build error (ADR-1235): a failed run whose only non-success jobs were never given a runner, ran into their `timeout-minutes` in a job that tests nothing, or were superseded by a newer commit is left out, as `gh run view <id> --json jobs` and each job's annotations classify it. Notes name the fix, its landing time, the window, each run left out and why, and the numbers. If no factory shift has run since the window's start, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Fix: 3edbb1559, pushed to work/dark-factory 2026-11-03 10:44:04 Arizona (17:44:04Z). A shift start no longer refuses over an orphan whose work cannot be told apart (the refusals at 17:25 and 17:40 on 2026-11-02 that kept the CI watch off until 17:54): it moves the task to Blocked and starts, after copying every lane branch ahead of the shift branch to a parked `factory/<ID>-lane-<n>-<stamp>` branch; the Blocked reason names the copies and the dirty lanes whose work each starting lane stashes (`Get-OrphanBlockReason` in DarkFactory/Park.ps1, called from DarkFactory/ShiftCoordinator.ps1). With 7ecfdd99f and BL-4650's 3a15c59d1, fewer tasks reach that state at all. `-TestPark`: 22/22 PASS under Windows PowerShell 5.1 and pwsh 7.
- Evidence still owed: the window starts at 2026-11-03T17:44:04Z and needs shift logs and CI runs written after it, so the task waits in Backlog for an interactive session to run the measurement.
- Measurement (interactive session, 2026-11-03 16:33 MST): `powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since '2026-11-03 10:44:04' -LogRoot Z:\repos\Curl.logs -CiRunsJson <gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500, read 16:33 MST>`. Window 2026-11-03T17:44:04Z to 23:33Z. It holds shift DarkFactory-20261103-080214 (polling the board with no lanes until 13:46 MST), -134646 (13 lanes, 13:46-14:52, ended by a restart) and -145254 (13 lanes from 14:53; lanes ended 16:14, coordinator in its merge's CI wait at the read), and 54 CI runs created since 17:36:45Z: 7 finished success or failure, 46 pending runs cancelled by newer pushes, 1 running.
- Numbers: ciRedMinutes 194.77; ciRedSpells 41688300433 18:20:55Z-20:23:53Z 122.97 min, 41715208450 21:35:59Z-22:47:47Z 71.8 min, 41735630431 from 23:19:11Z still red; tasksDone 45, tasksClaimedMoreThanOnce 0, requeues 0, unfinishedRuns 0.
- What the spells were: no test failed in any of them before 23:12Z. 41688300433 (28cbd2df) and 41723085402 (441bcff2, which kept the second spell red) failed only because the `Audit paths untouched by the dark factory` job's `actions/checkout` (fetch-depth 0) ran past its 5-minute `timeout-minutes`; filed as BL-4782 (ci.yml, audit branch). 41715208450 (2533dfc6) failed because its macOS `Build and test` job "was not acquired by Runner of type hosted even after multiple attempts". The first spell lasted 2 hours because the next run, 41691198001 (ac11d86f), waited until 20:19Z for a runner for its audit-guard job, with no lane running. The coordinator traced all three runs as "cannot read its log with gh run view --log-failed" and never classed them: a CI watch bug (an empty log read as `$null`), fixed by BL-4783 (6f289fef1). Run 41735630431 (fad49f8f) failed IdleTimeout_ContainerAsks1000msAndWaits2500_CurlSendsEmp~ on Windows at 23:12:58Z and is still red; the watch read the failed job at 16:13:38 MST, 40 s later.
- Verdict: criterion 1 does not hold. The expected result is no red spell over 30 minutes, and the window holds two (122.97 and 71.8 min) and a third still red. It also cannot hold as written: the window runs from the fix's landing, so these spells stay in it. Moved to Blocked: an interactive session (or Stewart) has to decide whether red spells with no failing test (GitHub runner capacity, the audit-guard timeout BL-4782) count against the finding's expected result. If they do not, the window gets restated to start after BL-4782 lands, a criterion change made in Backlog. Otherwise the process auditor's re-audit judges the finding on the mechanism evidence below.
- AF-0567's mechanism did not recur. Shift starts were not refused over an orphan, so the CI watch was not kept off and no stale known-failure line turned CI red. The shift starts at 13:46:56 and 14:52:58 MST went ahead with no refusal. The 14:52:53 restart left 12 tasks in Doing, and the next shift adopted each one in the lane that held it (see BL-4650). No KnownFailures line failed a CI run in the window. `-TestPark` 22/22 under Windows PowerShell 5.1 and pwsh 7.
- Build and fast tests (criterion 2): `dotnet build` 0 errors; `dotnet test --filter "TestCategory!=Integration"` 115 projects, 54,924 passed, 0 failed, at 7410f102c (2026-11-03 16:12 MST).

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-11-03: Doing -> Backlog. Fix landed (3edbb1559 at 2026-11-03 10:44 Arizona); the post-fix measurement needs shift logs written after it, so it waits in Backlog per its criterion.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-11-03: Doing -> Blocked. Criterion 1 cannot hold as written: the window from 2026-11-03T17:44:04Z holds CI red spells over 30 min (41688300433 18:20:55Z-20:23:53Z 122.97 min, 41715208450 21:35:59Z-22:47:47Z 71.8 min, 41735630431 from 23:19:11Z still red), from CI infrastructure (audit-guard timeout, BL-4782; macOS runner not acquired), not from the finding's mechanism, which did not recur. Needs an interactive session or Stewart to decide whether such spells count (restate the window after BL-4782 lands, in Backlog) or the process auditor's re-audit to judge.
- 2026-11-04: Blocked -> Backlog. ADR-1235 (Stewart approved 2026-11-04): CI red time counts only while a test or build fails, so criterion 1 is restated in Backlog and measured again under that rule.
- 2026-11-04: Backlog -> Doing. Criteria at claim: 2ab4542b 9742157d.
