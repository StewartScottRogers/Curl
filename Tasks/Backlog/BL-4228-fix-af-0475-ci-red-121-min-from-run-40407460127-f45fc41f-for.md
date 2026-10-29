---
id: BL-4228
title: Fix AF-0475: CI red 121 min from run 40407460127 (f45fc41f, Format_OpenSslUnprintableNames on Linux) while BL-3878 sat unclaimed
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-29
completed:
---
# BL-4228 — Fix AF-0475: CI red 121 min from run 40407460127 (f45fc41f, Format_OpenSslUnprintableNames on Linux) while BL-3878 sat unclaimed

## Goal

The cause of the factory behaviour the audit office reported as AF-0475 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0475 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0475-ci-red-121-min-from-run-40407460127-f45fc41f-forma.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

Spell 2026-10-26T22:15:32Z to 2026-10-27T00:16:54Z (15:15-17:16 Arizona), 121.37 min. Run 40407460127 failed on f45fc41f ('chore(tasks): finish BL-3637') with Format_OpenSslUnprintableNames_PrintsEmptySubjectAndIssuer (DarkFactory-20261026-063535.log:231). The failure was already filed as BL-3878 at 14:30:51 from an earlier run (DarkFactory-20261026-063535.log:195). It was claimed only at 16:24:43, by L2 of the next shift (DarkFactory-20261026-151632-L2.log:31). The task waited about 2 hours, across a shift change, while CI stayed red.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).ciRedSpells | ? runId -eq 40407460127
```

- Expected: No red spell over 30 minutes.
- Actual: minutes 121.37, runId 40407460127.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-29: Created.
