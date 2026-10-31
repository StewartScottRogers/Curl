---
id: BL-4408
title: Fix AF-0498: BL-4131 requeued twice (split for size, then a 106-minute 'shift deadline' run) and then Deferred and split again
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
# BL-4408 — Fix AF-0498: BL-4131 requeued twice (split for size, then a 106-minute 'shift deadline' run) and then Deferred and split again

## Goal

The cause of the factory behaviour the audit office reported as AF-0498 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0498 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0498-bl-4131-requeued-twice-split-for-size-then-a-106-m.md`.

Location: `logs/DarkFactory-20261030-060635-L13.log`

Location: `logs/DarkFactory-20261030-060635-L13.log`

L3 claimed it at 18:10:25 on 10-29 and requeued it at 18:56:56 ('Split for size: waits on BL-4303, BL-4304, BL-4305'). L13 claimed it at 06:18:52 on 10-30. That run took 67 turns, $4.15 and 106 min, and was requeued at 08:09:38: 'Shift deadline: code done and 99 tests pass, but the fast tests and a WebDavTools quality re-run ... are still to do'. Its result record says the code was 'uncommitted for the shift to stash'. L1 then split it over the cost cap at 15:41:38 into BL-4373 and BL-4374, and it was Deferred. That made two separate splits over two runs. Task cost $4.93 with no Done.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Z:/repos/Curl.audit/20261031-035829/logs/DarkFactory-*-L*.log -Pattern 'BL-4131 (claim|end|REQUEUE|split)'
```

- Expected: A task split once, early; a near-finished run commits its work.
- Actual: 2 REQUEUE (split for size; shift deadline after 105.6 min with the code uncommitted), then a second split and Deferred.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-31: Created.
