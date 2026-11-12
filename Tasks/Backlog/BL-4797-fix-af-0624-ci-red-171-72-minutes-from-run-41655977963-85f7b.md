---
id: BL-4797
title: Fix AF-0624: CI red 171.72 minutes from run 41655977963 (85f7b9d8): Linux-only progress-meter test failure, then failure logs the coordinator could not read while an idle coordinator stopped watching CI
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
# BL-4797 — Fix AF-0624: CI red 171.72 minutes from run 41655977963 (85f7b9d8): Linux-only progress-meter test failure, then failure logs the coordinator could not read while an idle coordinator stopped watching CI

## Goal

The cause of the factory behaviour the audit office reported as AF-0624 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0624 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0624-ci-red-171-72-minutes-from-run-41655977963-85f7b9d.md`.

Location: `logs/DarkFactory-20261103-080214.log:17`

Location: `logs/DarkFactory-20261103-080214.log:17`

ciRedSpells: 2026-11-03T15:01:02Z to 17:52:45Z (08:01 to 10:52 Arizona), 171.72 min, runId 41655977963. The run failed on Linux in CreateRunner_FileUrlToStandardOutputThatIsNotATerminal_WritesTheProgressMeter at commit 85f7b9d8 (BL-4729, a gRPC fix). BL-4732 was filed at 08:01 (c6ca89a8f), claimed on lane 1 at 08:02 and fixed at 08:04 (faccb10d4). The next run, 41660275371 on fe004e97, failed at 08:25. The coordinator logged 'merge not merged: CI failure on fe004e9' and 'run 41660275371 failure ... cannot read its log with gh run view --log-failed' (080214.log:16-17), filed nothing, and at 08:26 entered 'nothing claimable; polling the board every 5 minutes'. It wrote no further ci lines until 13:46. Runs 41669804192 (429184b1) and 41671248257 (cfbd85f7) also failed with nobody watching. CI turned green only at 10:52 Arizona, on run 41682710305 for f4a738a9, an interactive master merge. No merge to master went through while CI was red.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-03 -LogRoot Z:\repos\Curl.audit\20261103-161553\logs -CiRunsJson Z:\repos\Curl.audit\20261103-161553\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 41655977963; Select-String -Path Z:\repos\Curl.audit\20261103-161553\logs\DarkFactory-20261103-080214.log -Pattern ' ci | merge |polling'
```

- Expected: No red spell over 30 minutes; a CI fix task filed and landed for each failing run
- Actual: Spell 171.72 min; runs 41660275371, 41669804192, 41671248257 failed with no task filed; no ci lines between 08:26 and 13:46

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the window's start: the fix's landing, or for a task with no fix yet the later of its fix's landing and ADR-1235's commit), gives the finding's expected result with CI red time counted only while a run on the branch has a failing test or build error (ADR-1235): a failed run whose only non-success jobs were never given a runner, ran into their `timeout-minutes` in a job that tests nothing, or were superseded by a newer commit is left out, as `gh run view <id> --json jobs` and each job's annotations classify it. Notes name the fix, its landing time, the window, each run left out and why, and the numbers. If no factory shift has run since the window's start, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Measured 2026-11-06 over shift 20261106-051223: no infrastructure-only red time any more (BL-4846's capacity rerun turned run 42249533661 green 22 minutes after it failed). The one spell over 30 minutes, 94.52 min from run 42239087175, was a real Linux test failure (BL-4897), not runner capacity, so the expected 'no spell over 30 minutes' does not hold yet; left in Backlog for the next shift's logs.

- Cause found: two things. (1) The CI watch read the failed runs' empty failure logs as unreadable ("cannot read its log"), so it filed nothing; fixed by BL-4783 (6f289fef1, 2026-11-03 16:32:49 MST). (2) A -Continuous coordinator with nothing claimable polled the board every 5 minutes without running the CI watch at all (DarkFactory/ShiftCoordinator.ps1's idle poll), so shift 20261103-080214 wrote no `ci` line from 08:26 to 13:46 while runs 41660275371, 41669804192 and 41671248257 failed. The merge-wait poll ran the watch (BL-4412); the idle poll after it did not. It happened again on 2026-11-03/04: shift -204644 polled from 22:03 to 03:24 MST with no `ci` line, harmless only because CI stayed green.
- Fix: 3e4d0d43d, pushed to work/dark-factory 2026-11-04 05:24:14 MST (12:24:14Z). Each idle-poll look (`Invoke-IdlePollLook`) runs the CI watch, pulls the branch, and hands the ready CI fix to `Get-IdlePollDecision`, so a fix filed while the factory idles starts the next shift, past the -Hours deadline too (ADR-1158). RunDarkFactory.ps1's help says so. `-TestHeartbeat` 38/38 under Windows PowerShell 5.1 and pwsh 7, with the new cases "each idle-poll look runs the CI watch", "the look after the watch filed a CI fix names it claimable and the ready CI fix" and "that CI fix starts the next shift past the -Hours deadline".
- Window: from the fix, 2026-11-04T12:24:14Z (ADR-1235's commit b6dd48c99 landed in the same push). No factory shift has run since, so criterion 1 waits in Backlog. For reference, the same measurement from BL-4783's landing (`-Since '2026-11-03 16:32:49'`, gh run list read 2026-11-04 05:05 MST) gives ciRedMinutes 14.27, one spell (41764281440, 03:22:57Z-03:37:13Z, a Windows test failure filed as BL-4804), the same raw and under ADR-1235. AF-0624 itself is closed on the `audit` branch (`closed-how: reliable-reaudit`).
- Build and fast tests (criterion 2), 2026-11-04 at aef2fa6d2, which holds the fix: `dotnet build` 0 warnings, 0 errors; `dotnet test --filter "TestCategory!=Integration"` exit 0, no project failed.

Measured 2026-11-11 over today's shifts (20261111-082724, -101854, -190859; `Measure-FactoryProcess.ps1 -Since 2026-11-11 -LogRoot Z:\repos\Curl.logs` with a fresh `gh run list`): 5 red spells, 3 over 30 minutes - 131.8 min (run 43060203629, a real Quality gates failure, filed as BL-4969 22 minutes after the run ended), 74.0 min (run 43110153266, Quality gates, filed as BL-4995 6 minutes after) and 57.9 min (run 43127888162, the WriteOut_AfterADownload_ReportsEachVariable regression, filed as BL-4997/BL-4998 within a minute). No infrastructure-only red time, no failure left unread or unfiled, and every failure filed within minutes even across a shift hand-over, so the causes this finding named are fixed. The expected 'no spell over 30 minutes' still does not hold: CI's own run takes about 35 minutes (integration jobs), so any real regression's fix-and-rerun exceeds 30 minutes. Left in Backlog for the process auditor's re-audit to judge.

## Log

- 2026-11-03: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: 2ab4542b 9742157d.
- 2026-11-04: Doing -> Backlog. Fix landed 2026-11-04 05:24:14 MST (3e4d0d43d, the idle poll runs the CI watch); the window starts there and no factory shift has run since, so criterion 1 waits in Backlog for an interactive session to measure it.
