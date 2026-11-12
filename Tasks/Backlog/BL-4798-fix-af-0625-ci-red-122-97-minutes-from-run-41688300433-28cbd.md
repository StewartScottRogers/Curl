---
id: BL-4798
title: Fix AF-0625: CI red 122.97 minutes from run 41688300433 (28cbd2df): failure log unreadable and no shift watching CI
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
# BL-4798 — Fix AF-0625: CI red 122.97 minutes from run 41688300433 (28cbd2df): failure log unreadable and no shift watching CI

## Goal

The cause of the factory behaviour the audit office reported as AF-0625 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0625 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0625-ci-red-122-97-minutes-from-run-41688300433-28cbd2d.md`.

Location: `logs/DarkFactory-20261103-134646.log:17`

Location: `logs/DarkFactory-20261103-134646.log:17`

ciRedSpells: 2026-11-03T18:20:55Z to 20:23:53Z (11:20 to 13:23 Arizona), 122.97 min, runId 41688300433 on 28cbd2df, an interactive commit 'fix(factory): cancel a running CI run for lacking a fix only once it has failed'. The 080214 coordinator was idle and polling, with no ci lines from 08:26 to 13:46. The next shift's coordinator read the run at 13:51:04 and logged 'cannot read its log with gh run view --log-failed' (134646.log:17; again at 145254.log:31), so no task was filed. The spell ended on run 41691198001 for ac11d86f, which itself took 133 minutes (18:10Z to 20:23Z). The failing test is unknown because the coordinator could not read the log.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-03 -LogRoot Z:\repos\Curl.audit\20261103-161553\logs -CiRunsJson Z:\repos\Curl.audit\20261103-161553\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 41688300433; Select-String -Path Z:\repos\Curl.audit\20261103-161553\logs\DarkFactory-20261103-*.log -Pattern '41688300433'
```

- Expected: No red spell over 30 minutes; the failing test filed as a CI fix task
- Actual: Spell 122.97 min; coordinator lines say only 'cannot read its log with gh run view --log-failed'; no task filed

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] Every CI failure on `work/dark-factory` in a window of the live logs written after the fix landed is filed as a task, covered by a live task, or green again within 30 minutes of its run ending (Stewart, 2026-11-12: "every failure filed within 30 minutes", replacing "no red spell over 30 minutes", which CI's own ~35-minute run makes unreachable); Notes name the window and the slowest time.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Measured 2026-11-06 over shift 20261106-051223: no infrastructure-only red time any more (BL-4846's capacity rerun turned run 42249533661 green 22 minutes after it failed). The one spell over 30 minutes, 94.52 min from run 42239087175, was a real Linux test failure (BL-4897), not runner capacity, so the expected 'no spell over 30 minutes' does not hold yet; left in Backlog for the next shift's logs.

- Cause found: run 41688300433 (28cbd2df) had no failing test. Its non-success jobs (read 2026-11-04 with `gh run view 41688300433 --json jobs` and each job's annotations) were `Audit paths untouched by the dark factory`, cancelled after "The job has exceeded the maximum execution time of 5m0s" in its checkout, and `Audit self-tests (macos-latest)`, cancelled after "The job was not acquired by Runner of type hosted even after multiple attempts". Its failed-job log was empty, which the CI watch read as unreadable. Under ADR-1235 (b6dd48c99) this spell is infrastructure and does not count. The audit guard's checkout got a blobless fetch and 15 minutes (BL-4782, 624133845, audit branch); the empty-log reading was fixed by BL-4783 (6f289fef1, 2026-11-03 16:32:49 MST). The finding's second half, no shift watching CI, was the idle -Continuous coordinator polling without the CI watch (shift 20261103-080214, 08:26 to 13:46), the same cause as AF-0624.
- Fix: 3e4d0d43d (BL-4797), pushed to work/dark-factory 2026-11-04 05:24:14 MST (12:24:14Z): each idle-poll look runs the CI watch and hands the ready CI fix to the poll decision. `-TestHeartbeat` 38/38 under Windows PowerShell 5.1 and pwsh 7.
- Window: from the fix, 2026-11-04T12:24:14Z. No factory shift has run since, so criterion 1 waits in Backlog. For reference, from BL-4783's landing (`-Since '2026-11-03 16:32:49'`, gh run list read 2026-11-04 05:05 MST): ciRedMinutes 14.27, one spell (41764281440, 03:22:57Z-03:37:13Z), whose Windows test failure was filed as BL-4804; the same raw and under ADR-1235. AF-0625 itself is closed on the `audit` branch (`closed-how: reliable-reaudit`).
- Build and fast tests (criterion 2), 2026-11-04 at aef2fa6d2: `dotnet build` 0 warnings, 0 errors; `dotnet test --filter "TestCategory!=Integration"` exit 0, no project failed.

Measured 2026-11-11 over today's shifts (20261111-082724, -101854, -190859; `Measure-FactoryProcess.ps1 -Since 2026-11-11 -LogRoot Z:\repos\Curl.logs` with a fresh `gh run list`): 5 red spells, 3 over 30 minutes - 131.8 min (run 43060203629, a real Quality gates failure, filed as BL-4969 22 minutes after the run ended), 74.0 min (run 43110153266, Quality gates, filed as BL-4995 6 minutes after) and 57.9 min (run 43127888162, the WriteOut_AfterADownload_ReportsEachVariable regression, filed as BL-4997/BL-4998 within a minute). No infrastructure-only red time, no failure left unread or unfiled, and every failure filed within minutes even across a shift hand-over, so the causes this finding named are fixed. The expected 'no spell over 30 minutes' still does not hold: CI's own run takes about 35 minutes (integration jobs), so any real regression's fix-and-rerun exceeds 30 minutes. Left in Backlog for the process auditor's re-audit to judge.

Measured 2026-11-12 against the new criterion over 2026-11-11 15:00Z onward (shifts 20261111-101854 and -190859; `gh run list --workflow CI --branch work/dark-factory` and the coordinators' "ci" trace lines): 14 failed CI runs, every one read by the coordinator within 0-22 minutes of ending. The slowest was run 43060203629 (Quality gates; ended 10:05 Arizona, filed as BL-4969 at 10:27, 22 minutes). The rest were filed (BL-4974, BL-4995, BL-4997/BL-4998), covered by a live task, or green again within minutes. The criterion holds.

## Log

- 2026-11-03: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: 2ab4542b 9742157d.
- 2026-11-04: Doing -> Backlog. Fix landed 2026-11-04 05:24:14 MST (3e4d0d43d with BL-4783; 41688300433 had no failing test and does not count under ADR-1235); the window starts there and no factory shift has run since, so criterion 1 waits in Backlog for an interactive session to measure it.
