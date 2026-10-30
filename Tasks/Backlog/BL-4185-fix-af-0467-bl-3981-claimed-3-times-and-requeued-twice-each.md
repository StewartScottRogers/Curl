---
id: BL-4185
title: Fix AF-0467: BL-3981 claimed 3 times and requeued twice, each time on a missing dependency (BL-3976, then BL-3990)
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
# BL-4185 — Fix AF-0467: BL-3981 claimed 3 times and requeued twice, each time on a missing dependency (BL-3976, then BL-3990)

## Goal

The cause of the factory behaviour the audit office reported as AF-0467 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0467 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0467-bl-3981-claimed-3-times-and-requeued-twice-each-ti.md`.

Location: `logs/DarkFactory-20261028-201954-L3.log:37`

Location: `logs/DarkFactory-20261028-201954-L3.log:37`

Requeue 1 at 2026-10-28 12:45:43: 'criterion 2 needs BL-3976's ManagedMultiTransfer wiring (cycle broken...)' (DarkFactory-20261028-111517-L4.log:70). Requeue 2 at 21:24:50: 'Criterion 2 needs the 10 ms timer removed from ManagedMultiTransfer: BL-3990' (DarkFactory-20261028-201954-L3.log:37). Done on the third claim on 2026-10-29. There was a dependency cycle between BL-3976 and BL-3981 that the board did not catch before the claim. The tool reports claims=3, cost $2.47.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; Select-String -Path Z:/repos/Curl.audit/20261029-060658/logs/DarkFactory-*-L*.log -Pattern 'BL-3981 (claim|REQUEUE|DONE)'
```

- Expected: Fewer than 3 claims and fewer than 2 requeues.
- Actual: BL-3981 claims=3, 2 REQUEUE lines.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-29: Created.
