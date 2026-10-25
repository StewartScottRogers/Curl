---
id: BL-3457
title: Fix AF-0410: CI red 50 minutes on work/dark-factory from run 40034290028 (test1173 on Linux and macOS)
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-24
completed: 2026-10-25
---
# BL-3457 — Fix AF-0410: CI red 50 minutes on work/dark-factory from run 40034290028 (test1173 on Linux and macOS)

## Goal

The cause of the factory behaviour the audit office reported as AF-0410 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0410 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0410-ci-red-50-minutes-on-work-dark-factory-from-run-40.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells: 2026-10-23T22:57:04Z to 23:47:24Z, 50.33 min, run 40034290028 on 93ed8666. The coordinator filed BL-3420 at 16:00:26 local, 3 min after the red (DarkFactory-20261023-110315.log:118). L3 claimed it at 16:04:05 and it was Done at 16:13:29 ('test1173's man page scan runs in linear time'). Run 40035852439 on 710327b2, pushed before the fix, also failed. The first green run, 40038429558, was created at 23:13:30Z and finished at 23:47Z.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-23 -LogRoot Z:\repos\Curl.audit\20261023-232221\logs -CiRunsJson Z:\repos\Curl.audit\20261023-232221\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 40034290028
```

- Expected: no ciRedSpells entry over 30 minutes
- Actual: start 2026-10-23T22:57:04Z end 23:47:24Z minutes 50.33 runId 40034290028

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause: test1173 failed at 22:48:58Z and its job ended 22:50:02Z, but the run ended at 22:57:04Z (Quality gates) and the watch read only finished runs, so it filed at 23:00. The fix was Done 23:13:29Z; pre-fix run 40035852439 was running and always finishes on work/dark-factory (ADR-0693), so the fix's run 40038429558 waited until 23:22 to start and ended 23:47.
- Fix: 7ad52e292 (`DarkFactory/CiWatch.ps1`, ADR-0811, shared with BL-3365, BL-3454, BL-3455, BL-3456), landed on origin/work/dark-factory 2026-10-24T15:46:55Z (08:46:55 Arizona): `Get-CiRunningRun` files from the failed job (about 10 minutes sooner here), and `Stop-CiRunWithoutFix` would have cancelled 40035852439 at the fix's push (about 9 minutes sooner). `-TestCiWatch`: 60 PASS, 0 FAIL; `dotnet build` clean; fast tests green.
- To measure once a shift has run since then: the finding's measurement with `-Since 2026-10-24T15:46:55Z`, `-LogRoot` the repository's `.logs`, `-CiRunsJson` a fresh `gh run list` as above; expect no `ciRedSpells` entry over 30 minutes. The floor is one full CI run (25 minutes here), which only `.github/workflows/ci.yml` (a guard file, audit branch only) could shorten.
- Measured 2026-10-25 (interactive session, ADR-0761). Fix 7ad52e292, landed 2026-10-24T15:46:55Z (08:46:55 Arizona). Window: from then to the CI list's newest run, 40206727905, which ended 2026-10-25T15:48:25Z. Shifts in it: DarkFactory-20261024-185147, 20261025-021056, 035926, 074620 and 081939, which finished 37 tasks.
  - Commands: `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500 > <scratchpad>\ci-runs.json` (oldest run 2026-10-22). Then `powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since '2026-10-24 08:46:55' -LogRoot Z:\repos\Curl.logs -CiRunsJson <scratchpad>\ci-runs.json -OutFile <scratchpad>\process-ci.json` and `.ciRedSpells`.
  - Numbers: ciRedSpells is empty and ciRedMinutes is 0. The window holds 81 CI runs: 27 success, 54 cancelled, 0 failure.
  - No red spell occurred, so the fix's early failed-job read and cancel path were not exercised in this window. The criterion's expected result (no spell over 30 minutes) holds. The process auditor's re-audit is what closes the finding.

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. interactive session: CI watch fix for red spells Criteria at claim: 87cd20bd 9742157d.
- 2026-10-24: Doing -> Backlog. fix 7ad52e292 (ADR-0811) landed 2026-10-24T15:46:55Z; waits for a shift to run since then to measure the live logs (ADR-0761)
- 2026-10-25: Backlog -> Doing. Interactive session: measure the live CI after fix 7ad52e292 (ADR-0761) Criteria at claim: 87cd20bd 9742157d.
- 2026-10-25: Doing -> Done. Live CI after 7ad52e292 (from 2026-10-24T15:46:55Z, 5 shifts): ciRedSpells empty, 81 runs, 27 success, 54 cancelled, 0 failure
