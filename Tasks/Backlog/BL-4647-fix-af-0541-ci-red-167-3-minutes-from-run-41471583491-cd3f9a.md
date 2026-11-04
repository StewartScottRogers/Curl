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

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause found: the runs "cancelled by newer pushes" were only the pending runs each push replaces (ci.yml lets work/dark-factory's running run finish, ADR-0693). The red time after the fix came from run 41489970818 (13392545, started 17:18Z without BL-4513's fix): its Linux job failed at 17:40Z, but it held the concurrency slot until 18:44Z because its macOS job waited for a runner, and while a job waits GitHub reports the whole run as `queued`, not `in_progress`. The CI watch looked only for `in_progress`, so `Stop-CiRunWithoutFix` never saw a running run to cancel for lacking the landed CI fix (no "cancelled run" trace in DarkFactory-20261102-095114.log between 10:16 and 12:07), and `Get-CiRunningRun` never read its early failure. The same status was observed live on 2026-11-03 (run 41682710305: `queued` with three jobs `in_progress`).
- Fix: 73fa18c5a, pushed to work/dark-factory 2026-11-03 10:47:28 Arizona (17:47:28Z). `Get-CiActiveRuns` (DarkFactory/CiWatch.ps1) names the slot holder as the oldest unfinished run not `pending` and the waiting run as the newest other; `Stop-CiRunWithoutFix`, `Get-CiRunningRun` and `Invoke-CiWatch` use it. `-TestCiWatch`: 90/90 PASS under Windows PowerShell 5.1 and pwsh 7, with new cases for a `queued` holder.
- Evidence still owed: the window starts at 2026-11-03T17:47:28Z and needs shift logs and CI runs after it, so the task waits in Backlog for an interactive session to run the measurement.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-11-03: Doing -> Backlog. Fix landed (73fa18c5a at 2026-11-03 10:47 Arizona); the post-fix measurement needs shift logs and CI runs after it, so it waits in Backlog per its criterion.
