---
id: BL-4187
title: Fix AF-0469: BL-3754 claimed 3 times and requeued twice (files held by BL-4031/BL-4022, then a remainder split to BL-4067)
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
# BL-4187 — Fix AF-0469: BL-3754 claimed 3 times and requeued twice (files held by BL-4031/BL-4022, then a remainder split to BL-4067)

## Goal

The cause of the factory behaviour the audit office reported as AF-0469 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0469 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0469-bl-3754-claimed-3-times-and-requeued-twice-files-h.md`.

Location: `logs/DarkFactory-20261029-014637-L8.log:20`

Location: `logs/DarkFactory-20261029-014637-L8.log:20`

Requeue 1 at 2026-10-28 22:31:41, before any edit: 'Needs ITransferEvents/Output/Networking files held by BL-4031 and BL-4022; touches corrected (Curl.Tls is not where the lines live)' (DarkFactory-20261028-201954-L6.log:90). Its touches named the wrong library, so the overlap check could not hold it back. Requeue 2 at 2026-10-29 02:28:51: 'remaining close_notify/shutdown/store-cache lines are BL-4067' (DarkFactory-20261029-014637-L8.log:20). Done on claim 3. The tool reports claims=3, cost $1.28.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; Select-String -Path Z:/repos/Curl.audit/20261029-060658/logs/DarkFactory-*-L*.log -Pattern 'BL-3754 (claim|REQUEUE|DONE)'
```

- Expected: Fewer than 3 claims and fewer than 2 requeues.
- Actual: BL-3754 claims=3, 2 REQUEUE lines.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Fix eb3e574f6 (pushed to work/dark-factory 2026-10-30 about 08:40 Arizona, ADR-1155): a split for size files every part, the rest of the parent included, and Defers the parent instead of requeueing it; the deadline rule hands work back to be stashed instead of filing a task to wait on; a run stopped at the turn cap gets the one-shot split run at once; and a task whose log already shows a Doing -> Backlog requeue is split (or Blocked for triage) instead of being claimed a third time. Lanes load the factory at start, so measure from the first shift started after the push (the 14:07 hand-over at the latest). Measured before the fix (2026-10-29 to 2026-10-30 08:12): 61 REQUEUE lines, 25 split for size, 22 waits on another task (these already hold through depends-on), 5 out of time, 3 turn cap, 3 cost cap; claimed 3 times: BL-4170, BL-4124, BL-4132.

## Log

- 2026-10-29: Created.
