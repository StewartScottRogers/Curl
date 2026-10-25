---
id: BL-3455
title: Fix AF-0408: CI red 41 minutes on work/dark-factory from run 40008612979 (three VerifyPeer_CaCertificateFileHoldingOnly* partial-chain tests)
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
# BL-3455 — Fix AF-0408: CI red 41 minutes on work/dark-factory from run 40008612979 (three VerifyPeer_CaCertificateFileHoldingOnly* partial-chain tests)

## Goal

The cause of the factory behaviour the audit office reported as AF-0408 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0408 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0408-ci-red-41-minutes-on-work-dark-factory-from-run-40.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells: 2026-10-23T19:28:00Z to 20:08:59Z, 40.98 min, run 40008612979 on a07ed80b. The coordinator filed BL-3346, BL-3347 and BL-3348 at 12:34:23 local, 6 min after the red (DarkFactory-20261023-110315.log:52-55). L1 fixed them between 12:35:51 and 12:42:58 (BL-3347 closed as 'Already fixed by BL-3346'). A second failing run, 40011412660 on 9e379e12, was pushed before the fix. Green at 13:08 local.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-23 -LogRoot Z:\repos\Curl.audit\20261023-232221\logs -CiRunsJson Z:\repos\Curl.audit\20261023-232221\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 40008612979
```

- Expected: no ciRedSpells entry over 30 minutes
- Actual: start 2026-10-23T19:28:00Z end 20:08:59Z minutes 40.98 runId 40008612979

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause: the fix landed about 15 minutes after the red, but run 40011412660 on 9e379e12, a pre-fix commit, was already running and always finishes on work/dark-factory (ADR-0693); it failed too, and green came only from the run after it. The watch also filed 6 minutes after the run's end rather than when the failing job ended.
- Fix: 7ad52e292 (`DarkFactory/CiWatch.ps1`, ADR-0811, shared with BL-3365, BL-3454, BL-3456, BL-3457), landed on origin/work/dark-factory 2026-10-24T15:46:55Z (08:46:55 Arizona): `Stop-CiRunWithoutFix` cancels such a running run while CI is red once the waiting run's commit brings the CI-fix task into Tasks/Done, and `Get-CiRunningRun` files from a failed job before its run ends. `-TestCiWatch`: 60 PASS, 0 FAIL; `dotnet build` clean; fast tests green.
- To measure once a shift has run since then: the finding's measurement with `-Since 2026-10-24T15:46:55Z`, `-LogRoot` the repository's `.logs`, `-CiRunsJson` a fresh `gh run list` as above; expect no `ciRedSpells` entry over 30 minutes. The floor is one full CI run, which only `.github/workflows/ci.yml` (a guard file, audit branch only) could shorten.
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
