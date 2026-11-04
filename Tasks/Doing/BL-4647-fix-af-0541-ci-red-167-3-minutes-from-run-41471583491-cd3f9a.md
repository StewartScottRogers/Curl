---
id: BL-4647
title: Fix AF-0541: CI red 167.3 minutes from run 41471583491 (cd3f9a7d); 104 minutes of it came after the fix landed, because newer pushes cancelled every run
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
# BL-4647 — Fix AF-0541: CI red 167.3 minutes from run 41471583491 (cd3f9a7d); 104 minutes of it came after the fix landed, because newer pushes cancelled every run

## Goal

The cause of the factory behaviour the audit office reported as AF-0541 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0541 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0541-ci-red-167-3-minutes-from-run-41471583491-cd3f9a7d.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells: start 2026-11-02T16:19:10Z (09:19 Arizona), end 19:06:28Z (12:06), 167.3 min, runId 41471583491 on cd3f9a7d7 ('chore(tasks): split BL-4501...'). The coordinator could not read that run's failure log (DarkFactory-20261102-083146.log:23 at 09:21:19 and DarkFactory-20261102-095114.log at 09:57:47: 'cannot read its log with gh run view --log-failed'), so nothing was filed from it. The first readable failure was run 41479689790 on e9a9c5b9: ExecuteAsync_NtlmWithServiceNameAndNoCredentialsOffWindows_SendsNoAuthoriz..., which was added by 2d9fd3e51 (08:47, BL-4509, 'start NTLM for --service-name without -u on Windows only'). It passed on the Windows-only lane test and failed off Windows. BL-4513 was filed at 10:16:13 (57 min into the spell), claimed by L1 at 10:16:46 and pushed at 10:22:30. Run 41489970818 (13392545, started 10:18, before the fix) also failed. Then about 60 consecutive runs from 17:21Z to 18:43Z were cancelled by newer pushes, and none completed until 41503280775 (ef6501d3) went green at 19:06Z. So about 104 of the 167 minutes passed after the fix was on the branch, because lanes pushed every 2-3 minutes and kept cancelling the run.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-02 -LogRoot Z:\repos\Curl.audit\20261102-172448\logs -CiRunsJson Z:\repos\Curl.audit\20261102-172448\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object minutes -gt 30
```

- Expected: No red spell over 30 minutes
- Actual: runId 41471583491, start 2026-11-02T16:19:10Z, end 2026-11-02T19:06:28Z, minutes 167.3

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the window's start: the fix's landing, or for a task with no fix yet the later of its fix's landing and ADR-1235's commit), gives the finding's expected result with CI red time counted only while a run on the branch has a failing test or build error (ADR-1235): a failed run whose only non-success jobs were never given a runner, ran into their `timeout-minutes` in a job that tests nothing, or were superseded by a newer commit is left out, as `gh run view <id> --json jobs` and each job's annotations classify it. Notes name the fix, its landing time, the window, each run left out and why, and the numbers. If no factory shift has run since the window's start, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause found: the runs "cancelled by newer pushes" were only the pending runs each push replaces (ci.yml lets work/dark-factory's running run finish, ADR-0693). The red time after the fix came from run 41489970818 (13392545, started 17:18Z without BL-4513's fix): its Linux job failed at 17:40Z, but it held the concurrency slot until 18:44Z because its macOS job waited for a runner, and while a job waits GitHub reports the whole run as `queued`, not `in_progress`. The CI watch looked only for `in_progress`, so `Stop-CiRunWithoutFix` never saw a running run to cancel for lacking the landed CI fix (no "cancelled run" trace in DarkFactory-20261102-095114.log between 10:16 and 12:07), and `Get-CiRunningRun` never read its early failure. The same status was observed live on 2026-11-03 (run 41682710305: `queued` with three jobs `in_progress`).
- Fix: 73fa18c5a, pushed to work/dark-factory 2026-11-03 10:47:28 Arizona (17:47:28Z). `Get-CiActiveRuns` (DarkFactory/CiWatch.ps1) names the slot holder as the oldest unfinished run not `pending` and the waiting run as the newest other; `Stop-CiRunWithoutFix`, `Get-CiRunningRun` and `Invoke-CiWatch` use it. `-TestCiWatch`: 90/90 PASS under Windows PowerShell 5.1 and pwsh 7, with new cases for a `queued` holder.
- Evidence still owed: the window starts at 2026-11-03T17:47:28Z and needs shift logs and CI runs after it, so the task waits in Backlog for an interactive session to run the measurement.
- Measurement (interactive session, 2026-11-03 16:33 MST): `powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since '2026-11-03 10:47:28' -LogRoot Z:\repos\Curl.logs -CiRunsJson <gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500, read 16:33 MST>`. Window 2026-11-03T17:47:28Z to 23:33Z. It holds shift DarkFactory-20261103-080214 (polling the board with no lanes until 13:46 MST), -134646 (13 lanes, 13:46-14:52, ended by a restart) and -145254 (13 lanes from 14:53; lanes ended 16:14, coordinator in its merge's CI wait at the read), and 54 CI runs created since 17:36:45Z: 7 finished success or failure, 46 pending runs cancelled by newer pushes, 1 running.
- Numbers: ciRedMinutes 194.77; ciRedSpells 41688300433 18:20:55Z-20:23:53Z 122.97 min, 41715208450 21:35:59Z-22:47:47Z 71.8 min, 41735630431 from 23:19:11Z still red; tasksDone 45, tasksClaimedMoreThanOnce 0, requeues 0, unfinishedRuns 0.
- What the spells were: no test failed in any of them before 23:12Z. 41688300433 (28cbd2df) and 41723085402 (441bcff2, which kept the second spell red) failed only because the `Audit paths untouched by the dark factory` job's `actions/checkout` (fetch-depth 0) ran past its 5-minute `timeout-minutes`; filed as BL-4782 (ci.yml, audit branch). 41715208450 (2533dfc6) failed because its macOS `Build and test` job "was not acquired by Runner of type hosted even after multiple attempts". The first spell lasted 2 hours because the next run, 41691198001 (ac11d86f), waited until 20:19Z for a runner for its audit-guard job, with no lane running. The coordinator traced all three runs as "cannot read its log with gh run view --log-failed" and never classed them: a CI watch bug (an empty log read as `$null`), fixed by BL-4783 (6f289fef1). Run 41735630431 (fad49f8f) failed IdleTimeout_ContainerAsks1000msAndWaits2500_CurlSendsEmp~ on Windows at 23:12:58Z and is still red; the watch read the failed job at 16:13:38 MST, 40 s later.
- Verdict: criterion 1 does not hold. The expected result is no red spell over 30 minutes, and the window holds two (122.97 and 71.8 min) and a third still red. It also cannot hold as written: the window runs from the fix's landing, so these spells stay in it. Moved to Blocked: an interactive session (or Stewart) has to decide whether red spells with no failing test (GitHub runner capacity, the audit-guard timeout BL-4782) count against the finding's expected result. If they do not, the window gets restated to start after BL-4782 lands, a criterion change made in Backlog. Otherwise the process auditor's re-audit judges the finding on the mechanism evidence below.
- AF-0541's mechanism did not recur: no red time came after a fix had landed, and no running run that read `queued` was missed or left uncancelled. No CI fix landed while CI was red. The one CI-fix task in the window, BL-4780 (a flaky test a lane filed at 15:09 MST), came while CI was green. The coordinator cancelled no run (no "cancelled run" trace in DarkFactory-20261103-*.log since 10:47). `-TestCiWatch` 96/96 under Windows PowerShell 5.1 and pwsh 7, with the queued-holder cases.
- Build and fast tests (criterion 2): `dotnet build` 0 errors; `dotnet test --filter "TestCategory!=Integration"` 115 projects, 54,924 passed, 0 failed, at 7410f102c (2026-11-03 16:12 MST).

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-11-03: Doing -> Backlog. Fix landed (73fa18c5a at 2026-11-03 10:47 Arizona); the post-fix measurement needs shift logs and CI runs after it, so it waits in Backlog per its criterion.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-11-03: Doing -> Blocked. Criterion 1 cannot hold as written: the window from 2026-11-03T17:47:28Z holds CI red spells over 30 min (41688300433 18:20:55Z-20:23:53Z 122.97 min, 41715208450 21:35:59Z-22:47:47Z 71.8 min, 41735630431 from 23:19:11Z still red), from CI infrastructure (audit-guard timeout, BL-4782; macOS runner not acquired), not from the finding's mechanism, which did not recur. Needs an interactive session or Stewart to decide whether such spells count (restate the window after BL-4782 lands, in Backlog) or the process auditor's re-audit to judge.
- 2026-11-04: Blocked -> Backlog. ADR-1235 (Stewart approved 2026-11-04): CI red time counts only while a test or build fails, so criterion 1 is restated in Backlog and measured again under that rule.
- 2026-11-04: Backlog -> Doing. Criteria at claim: 2ab4542b 9742157d.
