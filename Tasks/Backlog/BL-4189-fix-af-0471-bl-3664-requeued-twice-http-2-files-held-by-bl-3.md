---
id: BL-4189
title: Fix AF-0471: BL-3664 requeued twice (HTTP/2 files held by BL-3667/BL-3668, then BL-3979 with the fix stashed) and then Deferred and split
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
# BL-4189 — Fix AF-0471: BL-3664 requeued twice (HTTP/2 files held by BL-3667/BL-3668, then BL-3979 with the fix stashed) and then Deferred and split

## Goal

The cause of the factory behaviour the audit office reported as AF-0471 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0471 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0471-bl-3664-requeued-twice-http-2-files-held-by-bl-366.md`.

Location: `logs/DarkFactory-20261028-111517-L2.log:53`

Location: `logs/DarkFactory-20261028-111517-L2.log:53`

Requeue 1 at 2026-10-26 18:58:55: 'Needs ...Http2StreamConnection.cs (held by BL-3667) and Http2Session.cs (held by BL-3668)' (DarkFactory-20261026-151632-L3.log:117). Requeue 2 at 2026-10-28 11:55:59: 'Waits on BL-3979 ... every other CGR-135 repro now matches REF, fix stashed' (DarkFactory-20261028-111517-L2.log:53). The task was then Deferred and split into BL-4004 and BL-4005. Work left in a stash risks being lost. Cost $3.76.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; Select-String -Path Z:/repos/Curl.audit/20261029-060658/logs/DarkFactory-*-L*.log -Pattern 'BL-3664 (claim|REQUEUE)'
```

- Expected: Fewer than 2 requeues.
- Actual: BL-3664: 2 REQUEUE lines, outcome requeued, then Deferred.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-29: Created.
