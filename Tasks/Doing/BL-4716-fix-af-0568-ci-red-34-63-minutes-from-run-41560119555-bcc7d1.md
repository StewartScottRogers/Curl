---
id: BL-4716
title: Fix AF-0568: CI red 34.63 minutes from run 41560119555 (bcc7d1f5): MQTT 5 conformance test Mqtt5s_UntrustedCertificate_IsRefusedBeforeAnyMqttWithExit60 failed on Linux and macOS
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
# BL-4716 — Fix AF-0568: CI red 34.63 minutes from run 41560119555 (bcc7d1f5): MQTT 5 conformance test Mqtt5s_UntrustedCertificate_IsRefusedBeforeAnyMqttWithExit60 failed on Linux and macOS

## Goal

The cause of the factory behaviour the audit office reported as AF-0568 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0568 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0568-ci-red-34-63-minutes-from-run-41560119555-bcc7d1f5.md`.

Location: `logs/DarkFactory-20261102-174304.log:58`

Location: `logs/DarkFactory-20261102-174304.log:58`

Spell 2026-11-03T01:43:57Z to 02:18:35Z, 34.63 minutes, from run 41560119555 on bcc7d1f5 (logs/ci-runs.json:709). BL-4596 was filed at 18:46:45 Arizona, claimed by L2 at 18:49:52 (opus, 20 turns, 8.6 minutes) and integrated at 19:02:41: 'The MQTT5 TLS refusal case counts a refusal after a completed handshake too, so it passes on Linux and macOS'. Run 41561329126 also failed. The coordinator cancelled 41562433776 at 19:05:35 because it lacked the fix. Run 41563931008 went green at 02:18:35Z. Lanes test only on Windows, so a platform-dependent conformance case reached CI unseen.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-02 -LogRoot Z:\repos\Curl.audit\20261103-002553\logs -CiRunsJson Z:\repos\Curl.audit\20261103-002553\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 41560119555; Select-String -Path Z:\repos\Curl.audit\20261103-002553\logs\DarkFactory-20261102-174304*.log -Pattern 'BL-4596'
```

- Expected: No CI red spell over 30 minutes.
- Actual: Spell 2026-11-03T01:43:57Z to 02:18:35Z, 34.63 minutes, runId 41560119555; BL-4596 filed 18:46:45, DONE 19:02:41.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the window's start: the fix's landing, or for a task with no fix yet the later of its fix's landing and ADR-1235's commit), gives the finding's expected result with CI red time counted only while a run on the branch has a failing test or build error (ADR-1235): a failed run whose only non-success jobs were never given a runner, ran into their `timeout-minutes` in a job that tests nothing, or were superseded by a newer commit is left out, as `gh run view <id> --json jobs` and each job's annotations classify it. Notes name the fix, its landing time, the window, each run left out and why, and the numbers. If no factory shift has run since the window's start, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause found: the factory's own steps worked (Linux failed 01:41:03Z, run ended 01:43:56Z, BL-4596 filed 01:46:45Z, fix integrated 02:02:41Z, run 41562433776 cancelled 02:05:35Z, green 02:18:35Z), but filing and the cancel each waited out the CI watch's 3-minute heartbeat. That lanes test only on Windows (the finding's root) cannot change in the factory; what can is how fast the watch reacts once CI is red.
- Fix: 5469c7711, pushed to work/dark-factory 2026-11-03 11:02:16 Arizona (18:02:16Z). While the newest run the watch read is red, it looks every minute instead of every heartbeat (`Get-CiWatchEveryMinutes`, `$CiRedWatchMinutes = 1`, DarkFactory/CiWatch.ps1), so a failure is filed, and a landed fix's running run cancelled, within a minute. The same commit keeps the cancel prompt: it replaces BL-4717's first rule (cancel only once the running run has failed, which would have let run 41562433776 run on) with one that skips only fixes naming a commit the running run already holds. `-TestCiWatch` 93/93 and `-TestHeartbeat` PASS under Windows PowerShell 5.1 and pwsh 7.
- Evidence still owed: the window starts at 2026-11-03T18:02:16Z and needs shift logs and CI runs after it, so the task waits in Backlog for an interactive session to run the measurement.
- Measurement (interactive session, 2026-11-03 16:33 MST): `powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since '2026-11-03 11:02:16' -LogRoot Z:\repos\Curl.logs -CiRunsJson <gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500, read 16:33 MST>`. Window 2026-11-03T18:02:16Z to 23:33Z. It holds shift DarkFactory-20261103-080214 (polling the board with no lanes until 13:46 MST), -134646 (13 lanes, 13:46-14:52, ended by a restart) and -145254 (13 lanes from 14:53; lanes ended 16:14, coordinator in its merge's CI wait at the read), and 54 CI runs created since 17:36:45Z: 7 finished success or failure, 46 pending runs cancelled by newer pushes, 1 running.
- Numbers: ciRedMinutes 71.8; ciRedSpells 41715208450 21:35:59Z-22:47:47Z 71.8 min, 41735630431 from 23:19:11Z still red; tasksDone 45, tasksClaimedMoreThanOnce 0, requeues 0, unfinishedRuns 0.
- What the spells were: no test failed in any of them before 23:12Z. 41688300433 (28cbd2df) and 41723085402 (441bcff2, which kept the second spell red) failed only because the `Audit paths untouched by the dark factory` job's `actions/checkout` (fetch-depth 0) ran past its 5-minute `timeout-minutes`; filed as BL-4782 (ci.yml, audit branch). 41715208450 (2533dfc6) failed because its macOS `Build and test` job "was not acquired by Runner of type hosted even after multiple attempts". The first spell lasted 2 hours because the next run, 41691198001 (ac11d86f), waited until 20:19Z for a runner for its audit-guard job, with no lane running. The coordinator traced all three runs as "cannot read its log with gh run view --log-failed" and never classed them: a CI watch bug (an empty log read as `$null`), fixed by BL-4783 (6f289fef1). Run 41735630431 (fad49f8f) failed IdleTimeout_ContainerAsks1000msAndWaits2500_CurlSendsEmp~ on Windows at 23:12:58Z and is still red; the watch read the failed job at 16:13:38 MST, 40 s later.
- Verdict: criterion 1 does not hold. The expected result is no red spell over 30 minutes, and the window holds one (71.8 min) and a second still red. It also cannot hold as written: the window runs from the fix's landing, so these spells stay in it. Moved to Blocked: an interactive session (or Stewart) has to decide whether red spells with no failing test (GitHub runner capacity, the audit-guard timeout BL-4782) count against the finding's expected result. If they do not, the window gets restated to start after BL-4782 lands, a criterion change made in Backlog. Otherwise the process auditor's re-audit judges the finding on the mechanism evidence below.
- AF-0568's mechanism (filing and the cancel each waiting out a 3-minute heartbeat) did not recur. Run 41735630431's Windows `Build and test` job failed at 16:12:58 MST, and the watch read it at 16:13:38, within the minute. It was a single-platform failure, so the watch waits to confirm it, and the lanes ended at 16:14. At the read it had not been filed, and the coordinator was in its merge's CI wait, whose polls run the watch. No fix landed while CI was red, so no cancel was due. `-TestCiWatch` 96/96 under Windows PowerShell 5.1 and pwsh 7, including "the watch looks every minute while CI is red".
- Build and fast tests (criterion 2): `dotnet build` 0 errors; `dotnet test --filter "TestCategory!=Integration"` 115 projects, 54,924 passed, 0 failed, at 7410f102c (2026-11-03 16:12 MST).

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-11-03: Doing -> Backlog. Fix landed (5469c7711 at 2026-11-03 11:02 Arizona); the post-fix measurement needs shift logs and CI runs after it, so it waits in Backlog per its criterion.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-11-03: Doing -> Blocked. Criterion 1 cannot hold as written: the window from 2026-11-03T18:02:16Z holds CI red spells over 30 min (41715208450 21:35:59Z-22:47:47Z 71.8 min, 41735630431 from 23:19:11Z still red), from CI infrastructure (audit-guard timeout, BL-4782; macOS runner not acquired), not from the finding's mechanism, which did not recur. Needs an interactive session or Stewart to decide whether such spells count (restate the window after BL-4782 lands, in Backlog) or the process auditor's re-audit to judge.
- 2026-11-04: Blocked -> Backlog. ADR-1235 (Stewart approved 2026-11-04): CI red time counts only while a test or build fails, so criterion 1 is restated in Backlog and measured again under that rule.
- 2026-11-04: Backlog -> Doing. Criteria at claim: 2ab4542b 9742157d.
