---
id: BL-4414
title: Fix AF-0504: CI red 486 min from run 41071219626 (302d7dd3): the quality gates, then a run of five further failures
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
# BL-4414 — Fix AF-0504: CI red 486 min from run 41071219626 (302d7dd3): the quality gates, then a run of five further failures

## Goal

The cause of the factory behaviour the audit office reported as AF-0504 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0504 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0504-ci-red-486-min-from-run-41071219626-302d7dd3-the-q.md`.

Location: `logs/DarkFactory-20261030-060635.log`

Location: `logs/DarkFactory-20261030-060635.log`

ciRedSpells: 2026-10-30T14:42:52Z to 22:48:59Z, 486.12 min, runId 41071219626, headSha 302d7dd3 (07:42-15:48 MST). Six fix tasks were filed in turn. BL-4349 (Quality gates windows-latest): filed 07:52:26, claimed 08:08:28, Done 09:03:11. BL-4359 (Pack Curl.Mcp macos-latest, upload-artifact; its own Done note calls it a 'Transient GitHub artifact-service timeout'): 11:39:14, Done 11:43:30. BL-4360 (flaky quality gates): 12:31:36, Done 12:39:39. BL-4368 (ConnectAsync_WithAnOpenerOnAClosedPort_...): 14:02:42, Done 14:09:17. BL-4369 (the same test again): 14:24:06, Done 15:41:00. BL-4370 (ConnectAsync_ToARealRefusedLoopbackPortOffWindows_...): 14:27:09, Done 15:29:57. Each fix landed and the next failure surfaced. BL-4368's refused-port fix did not hold, so BL-4369 was filed for the same test.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-29 -LogRoot Z:/repos/Curl.audit/20261031-035829/logs -CiRunsJson Z:/repos/Curl.audit/20261031-035829/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).ciRedSpells | ? runId -eq 41071219626
```

- Expected: No red spell over 30 minutes.
- Actual: start 2026-10-30T14:42:52Z end 2026-10-30T22:48:59Z minutes 486.12 runId 41071219626

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-31: Created.
