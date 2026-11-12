---
id: BL-4846
title: Fix AF-0664: CI red 171.7 minutes from run 41655977963 (85f7b9d8), though its failing test was fixed within 4 minutes
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-11-04
completed:
---
# BL-4846 — Fix AF-0664: CI red 171.7 minutes from run 41655977963 (85f7b9d8), though its failing test was fixed within 4 minutes

## Goal

The cause of the factory behaviour the audit office reported as AF-0664 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0664 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0664-ci-red-171-7-minutes-from-run-41655977963-85f7b9d8.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells: start 2026-11-03T15:01:02Z, end 17:52:45Z, 171.72 min, runId 41655977963, headSha 85f7b9d8 (BL-4729, fix(grpc)). Failing test: CreateRunner_FileUrlToStandardOutputThatIsNotATerminal_WritesTheProgressMeter on Linux ('Expected strings to be equal'; on a slow runner the progress meter added a one-second refresh line). The CI watch filed BL-4732 at 15:01:54Z (c6ca89a8f, about 1 minute after the failure). Lane 1 claimed it at 15:02:34Z (8ef1d6fe2), and the fix faccb10d4 landed at 15:04:17Z. The other ~165 minutes were three runs whose only non-success jobs were infrastructure, which the tool lists in ciInfrastructureRuns (ADR-1235): 41660275371 (fe004e97) and 41669804192 (429184b1), macOS runner not acquired; 41671248257 (cfbd85f7), the same plus an audit-guard timeout. No CI run was created on the branch from 16:09:22Z to 17:17:00Z. The spell ended only with the next success, 41682710305 (f4a738a9), at 17:52:45Z. So the test-caused red lasted about 4 minutes; the spell's length is the time left with no successful run. Under ADR-1235 those runs neither add red time nor end the spell.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-03 -LogRoot Z:\repos\Curl.audit\20261104-064338\logs -CiRunsJson Z:\repos\Curl.audit\20261104-064338\logs\ci-runs.json -OutFile $env:TEMP\process.json; $j = Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json; $j.ciRedSpells | Where-Object runId -eq 41655977963; $j.ciInfrastructureRuns | Select-Object -First 3
```

- Expected: No spell over 30 minutes.
- Actual: start 2026-11-03T15:01:02Z end 2026-11-03T17:52:45Z minutes 171.72 runId 41655977963; infrastructure-only runs 41660275371, 41669804192 and 41671248257 inside the spell

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] Every CI failure on `work/dark-factory` in a window of the live logs written after the fix landed is filed as a task, covered by a live task, or green again within 30 minutes of its run ending (Stewart, 2026-11-12: "every failure filed within 30 minutes", replacing "no red spell over 30 minutes", which CI's own ~35-minute run makes unreachable); Notes name the window and the slowest time.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Measured 2026-11-06 over shift 20261106-051223: no infrastructure-only red time any more (BL-4846's capacity rerun turned run 42249533661 green 22 minutes after it failed). The one spell over 30 minutes, 94.52 min from run 42239087175, was a real Linux test failure (BL-4897), not runner capacity, so the expected 'no spell over 30 minutes' does not hold yet; left in Backlog for the next shift's logs.

Fix (2026-11-04, interactive): the CI watch now reruns the failed jobs of the newest CI run on the branch whose only bad jobs never got a runner (`gh run rerun --failed`, at most twice per run, `Invoke-CiCapacityRerun` in `DarkFactory/CiWatch.ps1`), and reads the rerun afresh. AF-0664's spell was 165 minutes of such runs waiting for a push that got every runner. -TestCiWatch passes under Windows PowerShell and pwsh (three new BL-4846 cases); `dotnet build` clean and the fast tests pass (115 projects, 0 failed). Criterion 1 waits for a factory shift after the fix lands, so the task goes back to Backlog until then.

Measured 2026-11-11 over today's shifts (20261111-082724, -101854, -190859; `Measure-FactoryProcess.ps1 -Since 2026-11-11 -LogRoot Z:\repos\Curl.logs` with a fresh `gh run list`): 5 red spells, 3 over 30 minutes - 131.8 min (run 43060203629, a real Quality gates failure, filed as BL-4969 22 minutes after the run ended), 74.0 min (run 43110153266, Quality gates, filed as BL-4995 6 minutes after) and 57.9 min (run 43127888162, the WriteOut_AfterADownload_ReportsEachVariable regression, filed as BL-4997/BL-4998 within a minute). No infrastructure-only red time, no failure left unread or unfiled, and every failure filed within minutes even across a shift hand-over, so the causes this finding named are fixed. The expected 'no spell over 30 minutes' still does not hold: CI's own run takes about 35 minutes (integration jobs), so any real regression's fix-and-rerun exceeds 30 minutes. Left in Backlog for the process auditor's re-audit to judge.

Measured 2026-11-12 against the new criterion over 2026-11-11 15:00Z onward (shifts 20261111-101854 and -190859; `gh run list --workflow CI --branch work/dark-factory` and the coordinators' "ci" trace lines): 14 failed CI runs, every one read by the coordinator within 0-22 minutes of ending. The slowest was run 43060203629 (Quality gates; ended 10:05 Arizona, filed as BL-4969 at 10:27, 22 minutes). The rest were filed (BL-4974, BL-4995, BL-4997/BL-4998), covered by a live task, or green again within minutes. The criterion holds.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-11-04: Doing -> Backlog. Fix landed; criterion 1 waits for a factory shift's logs after it.
