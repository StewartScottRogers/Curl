---
id: BL-3364
title: Fix AF-0387: BL-3204 requeued twice: first blocked by BL-3199 holding ManagedEasyTransfer.cs, then out of its cost cap with 7 Engine tests failing
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-23
completed:
---
# BL-3364 — Fix AF-0387: BL-3204 requeued twice: first blocked by BL-3199 holding ManagedEasyTransfer.cs, then out of its cost cap with 7 Engine tests failing

## Goal

The cause of the factory behaviour the audit office reported as AF-0387 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0387 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0387-bl-3204-requeued-twice-first-blocked-by-bl-3199-ho.md`.

Location: `logs/DarkFactory-20261023-005934-L2.log:15`

Location: `logs/DarkFactory-20261023-005934-L2.log:15`

First claim, L7 at 22:48:46: requeued at 22:52:55 after 19 Opus turns ($0.86) and no code, because 'The fix needs Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, and BL-3199, which is in Doing, holds that file' (BL-3204-20261022-192710-L7.jsonl). Second claim, L2 at 01:00:14 as an Opus retry: requeued at 01:07:44 after 38 turns ($1.75), 'Out of this run's cost cap with 7 Engine unit tests still failing'. Its uncommitted work was stashed (L2.log:13). At 03:51:46 L1 found it unclaimable over its cost cap ($2.60 of $3.72) and split it into BL-3271..BL-3273. BL-3271 (L2, 03:54:18 to 04:21:49, $2.91) landed the stashed work. Its first test run, at 03:55:48, shows the same 7 failures (2376/2383), so the stash was recovered and no work was lost. Total: $5.5 and 4 runs for one change.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261023-061815\logs -CiRunsJson Z:\repos\Curl.audit\20261023-061815\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-061815\logs\DarkFactory-*.log -Pattern 'BL-3204 (claim|REQUEUE|stash|split|unclaimable)'
```

- Expected: At most one requeue for BL-3204.
- Actual: Two REQUEUE lines (22:52:55 L7 and 01:07:44 L2), then a cost-cap split at 03:53:43.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-24: Backlog -> Doing. Interactive session: fixing the prerequisite-requeue cost-cap mechanism (ADR-0811) Criteria at claim: 87cd20bd 9742157d.
