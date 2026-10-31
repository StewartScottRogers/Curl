---
id: BL-4404
title: Fix AF-0494: BL-4170 claimed 3 times and requeued twice on the run budget with work stashed; its split's push was refused
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
# BL-4404 — Fix AF-0494: BL-4170 claimed 3 times and requeued twice on the run budget with work stashed; its split's push was refused

## Goal

The cause of the factory behaviour the audit office reported as AF-0494 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0494 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0494-bl-4170-claimed-3-times-and-requeued-twice-on-the.md`.

Location: `logs/DarkFactory-20261029-140509-L1.log`

Location: `logs/DarkFactory-20261029-140509-L1.log`

Claims: L5 09:18:58, which ended after 16 turns and 5.5 min and was requeued at 09:24:52 ('Run budget spent after the tokenizer (left uncommitted for the shift's stash)'); L1 14:16:16, which ended after 21 turns and 8.1 min and was requeued at 14:29:53 ('Run budget spent after the parser and descriptors (uncommitted for the shift's stash)'). Then L10 at 14:11:51 logged 'split over its cost cap; one split run' and at 14:31:39 'failed: the push of the split was refused; blocked'. The task file records Backlog -> Blocked ('cost cap ... split the task; the automatic split failed: the push of the split was refused'), then Blocked -> Backlog when the cost-cap window passed. A third claim, L11 at 20:26:54, ran 39 turns and was Done at 20:51:26. That breaks ADR-1155's two-claim limit. Total cost $4.80 over three claims for a task one 39-turn run finished.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Z:/repos/Curl.audit/20261031-035829/logs/DarkFactory-*-L*.log -Pattern 'BL-4170 (claim|end|REQUEUE|DONE|split)'
```

- Expected: At most two claims; a failed split does not fall back to a third full claim.
- Actual: 3 claims, 2 REQUEUE (run budget, work stashed), a failed split push, then a third claim that finished.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-31 (interactive): measured with Measure-FactoryProcess.ps1 over a snapshot of Z:/repos/Curl.logs (files written since 2026-10-30 12:00; the one live run BL-4420-L1 left out as running) with a fresh 500-run CI list, at about 11:10 Arizona 2026-10-31. Since fix eb3e574f6 (ADR-1155, 10-30 08:40): tasksClaimedMoreThanOnce 0, requeues 4 (each on a task claimed once), no turn-cap stop and no split run in the coordinator logs, unfinishedRuns empty; 42 tasks done, median 12.0 min, $0.94 per task. The incident the finding names predates the fix.

## Log

- 2026-10-31: Created.
