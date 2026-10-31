---
id: BL-4415
title: Fix AF-0505: CI red 76 min from run 41151657472 (dbc468ec) unseen: every coordinator from 15:24 on 10-30 could not list CI runs with gh
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-31
completed:
---
# BL-4415 — Fix AF-0505: CI red 76 min from run 41151657472 (dbc468ec) unseen: every coordinator from 15:24 on 10-30 could not list CI runs with gh

## Goal

The cause of the factory behaviour the audit office reported as AF-0505 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0505 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0505-ci-red-76-min-from-run-41151657472-dbc468ec-unseen.md`.

Location: `logs/DarkFactory-20261030-193800.log`

Location: `logs/DarkFactory-20261030-193800.log`

ciRedSpells: 2026-10-31T01:33:01Z to 02:49:10Z, 76.15 min, runId 41151657472, headSha dbc468ec ('BL-3934 done - the repository is workable on Linux and macOS'), 18:33-19:49 MST 10-30. Every coordinator from 15:24 on logs 'ci cannot list the CI runs on work/dark-factory with gh': DarkFactory-20261030-152226.log:9 (15:24:01), -193026.log (19:31:28), -193800.log (19:38:28), -205631.log (20:56:58), -205849.log (20:59:17), -220408.log (22:05:11) and -20261031-030114.log (03:02:25). No 'ci filed' line appears after BL-4370 at 14:27:09, so this spell got no fix task, and the CI watch (BL-987) stayed blind for about 12 hours of shifts without raising an alarm.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-29 -LogRoot Z:/repos/Curl.audit/20261031-035829/logs -CiRunsJson Z:/repos/Curl.audit/20261031-035829/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).ciRedSpells | ? runId -eq 41151657472; Select-String -Path Z:/repos/Curl.audit/20261031-035829/logs/DarkFactory-2*.log -Pattern 'cannot list the CI runs'
```

- Expected: No red spell over 30 minutes; the coordinator lists CI runs on every heartbeat.
- Actual: minutes 76.15 runId 41151657472; 8 'cannot list the CI runs on work/dark-factory with gh' lines from 2026-10-30 15:24 on.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-31 (interactive): measured with Measure-FactoryProcess.ps1 over a snapshot of Z:/repos/Curl.logs (files written since 2026-10-30 12:00; the one live run BL-4420-L1 left out as running) with a fresh 500-run CI list, at about 11:10 Arizona 2026-10-31. The cause, every coordinator unable to list CI runs from 10-30 (the timed gh wrapper space-joined --json a,b), was fixed by 8c14126f5 (BL-4384), first running in the 06:08 shift on 10-31 (DarkFactory-20261031-060833.log lists the runs at 06:09). ciRedSpells since then: none; the only spells over 30 min (44 and 76 min) were on 10-30, during the blind spell.

## Log

- 2026-10-31: Created.
