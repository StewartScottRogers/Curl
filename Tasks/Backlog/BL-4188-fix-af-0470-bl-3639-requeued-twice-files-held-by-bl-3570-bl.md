---
id: BL-4188
title: Fix AF-0470: BL-3639 requeued twice (files held by BL-3570/BL-3893, then the 80-turn cap) and then Deferred and split
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-29
completed:
---
# BL-4188 — Fix AF-0470: BL-3639 requeued twice (files held by BL-3570/BL-3893, then the 80-turn cap) and then Deferred and split

## Goal

The cause of the factory behaviour the audit office reported as AF-0470 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0470 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0470-bl-3639-requeued-twice-files-held-by-bl-3570-bl-38.md`.

Location: `logs/DarkFactory-20261028-052757-L9.log:20`

Location: `logs/DarkFactory-20261028-052757-L9.log:20`

Requeue 1 at 2026-10-26 17:55:48 after 27 turns: 'Waiting on BL-3570 (holds ...CommandLineOptionTable.cs) and BL-3893 (holds ...CommandLineRefusal.cs)' (DarkFactory-20261026-151632-L6.log:104). Requeue 2 at 2026-10-28 06:28:00: 81 turns in 14.2 min, stopped at the 80-turn cap (DarkFactory-20261028-052757-L9.log:20). The task was then Deferred and split into BL-3972, BL-3973 and BL-3974. Cost $5.30.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; Select-String -Path Z:/repos/Curl.audit/20261029-060658/logs/DarkFactory-*-L*.log -Pattern 'BL-3639 (claim|end|REQUEUE)'
```

- Expected: Fewer than 2 requeues.
- Actual: BL-3639: 2 REQUEUE lines, outcome requeued, then Deferred.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-29: Created.
