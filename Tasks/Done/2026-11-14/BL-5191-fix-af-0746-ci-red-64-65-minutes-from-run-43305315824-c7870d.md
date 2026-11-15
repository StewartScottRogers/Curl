---
id: BL-5191
title: Fix AF-0746: CI red 64.65 minutes from run 43305315824 (c7870da8) with no coordinator trace in the log window
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-11-14
completed: 2026-11-14
---
# BL-5191 — Fix AF-0746: CI red 64.65 minutes from run 43305315824 (c7870da8) with no coordinator trace in the log window

## Goal

The cause of the factory behaviour the audit office reported as AF-0746 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0746 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0746-ci-red-64-65-minutes-from-run-43305315824-c7870da8.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

Run 43305315824 failed, ended 2026-11-12T21:14:44Z (14:14 local). The spell ran to 22:19:23Z, 64.65 minutes. No ci trace line names the run in any DarkFactory log, since the first log starts at 20:00 local, after the run. The next green run came more than 30 minutes after.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-12 -LogRoot <logs> -CiRunsJson <logs>/ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 43305315824
```

- Expected: No spell, or the failure filed within 30 minutes of the run ending.
- Actual: Spell of 64.65 minutes.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The incident itself: run 43305315824 ended 2026-11-12 14:14:44 Arizona; the coordinator read it at 14:05:13 while it ran and filed BL-5051 at 14:10:52, before it ended.
- Fix: none needed in the factory. The original incident was a measurement artefact: the coordinator did trace and file the run, in `DarkFactory-20261112-130616.log`, which the audit's log copy (starting 2026-11-12 20:00 Arizona) did not hold while its CI run list reached further back. The audit-side fix is PR #343 (`Audit/Instructions/Process.md`: a re-audit's window starts at the first copied log, and older CI runs are left out). The coordinator's CI watch fix it is measured against is BL-5089..BL-5091, landed 2026-11-13 06:48 Arizona (ef0df5240).
- Window measured: CI runs and coordinator `ci` trace lines from 2026-11-13 06:48 to 2026-11-14 12:00 Arizona (`Measure-FactoryProcess.ps1 -Since 2026-11-13 -LogRoot Z:\repos\Curl.logs` with a fresh `gh run list --workflow CI --branch work/dark-factory ... --limit 500`).
- Numbers: 11 failed CI runs that were not infrastructure-only. The first coordinator line naming each came between 9.8 minutes before the run ended (a running run's failed jobs, ADR-0811) and 3.3 minutes after it, and each was filed (BL-5144, BL-5150, BL-5151, BL-5159, BL-5160, BL-5167, BL-5171, BL-5193) or covered by one of those live tasks. Slowest run end to filed task: about 10 minutes (run 43574261401, ended 01:21, BL-5171 filed 01:30). Six red spells: 67.1, 109.8, 27.5, 19.8, 12.3 and 13.8 minutes; the two over 30 minutes had their failures filed within 2 minutes and stayed red only while the fix and CI's own ~35-minute run went through. Expected result "no spell, or the failure filed within 30 minutes of the run ending": holds for every run.
- Build and fast tests at 03e30b8a1f: `dotnet build` 0 errors; `dotnet test --filter "TestCategory!=Integration"` exit 0, no project failed.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-11-14: Doing -> Done. AF-0746's run was filed within 30 minutes (BL-5051, before it ended); since BL-5089..5091 all 11 failed runs were filed or covered within 10 minutes; audit-side window fix is PR #343
