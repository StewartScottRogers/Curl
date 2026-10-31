---
id: BL-4183
title: Fix AF-0465: BL-3563 claimed 3 times and requeued 3 times before it was split and Deferred
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
# BL-4183 — Fix AF-0465: BL-3563 claimed 3 times and requeued 3 times before it was split and Deferred

## Goal

The cause of the factory behaviour the audit office reported as AF-0465 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0465 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0465-bl-3563-claimed-3-times-and-requeued-3-times-befor.md`.

Location: `logs/DarkFactory-20261028-052757-L1.log:53`

Location: `logs/DarkFactory-20261028-052757-L1.log:53`

Claim 1 (L5, 2026-10-26 09:30:59) ran 17 turns and was requeued at 09:39:14: 'Needs BL-3846 ... and BL-3847 first' (DarkFactory-20261026-063535-L5.log:152). Claim 2 (L5, 15:19:03) ran 62 turns over 33.7 min and was requeued at 15:57:12: 'Needs BL-3890 ... the multi wiring is in the stash' (DarkFactory-20261026-151632-L5.log:33). Claim 3 (L1, 2026-10-28 06:01:02) ran 81 turns over 53.3 min and was requeued at 06:56:09 on the 80-turn cap (DarkFactory-20261028-052757-L1.log:53). The task was then Deferred and split into BL-3976, BL-3977 and BL-3978. The dependencies were found by running the task, not before claiming it, and the split came only after a capped run. Total cost $9.19.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; Select-String -Path Z:/repos/Curl.audit/20261029-060658/logs/DarkFactory-*-L*.log -Pattern 'BL-3563 (claim|end|REQUEUE)'
```

- Expected: A task claimed fewer than 3 times and requeued fewer than twice.
- Actual: BL-3563 claims=3, 3 REQUEUE lines, outcome requeued, then Deferred and split.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-29: Created.
