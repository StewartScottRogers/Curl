---
id: BL-4405
title: Fix AF-0495: BL-4124 claimed 3 times: split for size, then a lane-12 rebase conflict the resolver could not settle
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
# BL-4405 — Fix AF-0495: BL-4124 claimed 3 times: split for size, then a lane-12 rebase conflict the resolver could not settle

## Goal

The cause of the factory behaviour the audit office reported as AF-0495 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0495 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0495-bl-4124-claimed-3-times-split-for-size-then-a-lane.md`.

Location: `logs/DarkFactory-20261029-140509-L12.log`

Location: `logs/DarkFactory-20261029-140509-L12.log`

L1 claimed it at 10:17:14 and requeued it at 10:20:59 ('Split for size: waits on BL-4197, BL-4198, BL-4199'). L12 claimed it at 14:14:18 and ran four runs (60 turns/44 min, then 10/6.9, 10/5.9 and 12/8.8 min). At 16:08:23 it was requeued: 'Lane 12 could not integrate: rebase conflict the resolver could not settle. The work is on branch factory/BL-4124-lane-12-20261029-140509'. L11 claimed it a third time at 16:09:54 and ran four more runs (14 turns/37.9 min, 13/11.4, 16/8.8, 24/6.2) before DONE at 18:17:45. Total 127.85 min and $4.81. A third claim, despite ADR-1155.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Z:/repos/Curl.audit/20261031-035829/logs/DarkFactory-*-L*.log -Pattern 'BL-4124 (claim|end|REQUEUE|DONE)'
```

- Expected: A task claimed at most twice; an integration conflict resolved without a fresh claim.
- Actual: 3 claims, REQUEUE on split for size and on an unresolved rebase conflict.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-31: Created.
