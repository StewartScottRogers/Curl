---
id: BL-4184
title: Fix AF-0466: BL-3880 claimed 3 times and requeued twice (split for size, then a file held by another task)
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
# BL-4184 — Fix AF-0466: BL-3880 claimed 3 times and requeued twice (split for size, then a file held by another task)

## Goal

The cause of the factory behaviour the audit office reported as AF-0466 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0466 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0466-bl-3880-claimed-3-times-and-requeued-twice-split-f.md`.

Location: `logs/DarkFactory-20261028-111517-L4.log:27`

Location: `logs/DarkFactory-20261028-111517-L4.log:27`

The task Log shows 3 claims on 2026-10-28. Requeue 1 at 09:42:20: 'Split for size: waits on BL-3948 ... BL-3952' (DarkFactory-20261028-052757-L4.log:137). Requeue 2 at 11:52:36: 'Waits on BL-3975 (-m writes its timeout line twice; HttpProtocolHandler.cs, held by BL-3874)' (DarkFactory-20261028-111517-L4.log:27). Claim 3 finished Done. The tool reports claims=3, cost $2.39.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; Select-String -Path Z:/repos/Curl.audit/20261029-060658/logs/DarkFactory-*-L*.log -Pattern 'BL-3880 (claim|REQUEUE|DONE)'
```

- Expected: Fewer than 3 claims and fewer than 2 requeues.
- Actual: BL-3880 claims=3, 2 REQUEUE lines.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-29: Created.
