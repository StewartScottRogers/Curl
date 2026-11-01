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
completed: 2026-10-31
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

- [x] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-31 (interactive): same cause and fix as BL-4406 (692da47a4). Waits for a shift on the fix before its measurement can be taken.
- 2026-10-31 (interactive): measured with Measure-FactoryProcess.ps1 over a snapshot of Z:/repos/Curl.logs (files written since 2026-10-30 12:00; the one live run BL-4420-L1 left out as running) with a fresh 500-run CI list, at about 11:10 Arizona 2026-10-31. No task split twice since eb3e574f6, but a near-finished run still stashed its work: BL-4377 (lane 2, 10-31 03:16) left built-and-tested code uncommitted, which cost a split (BL-4384, BL-4385) to recover. Stays open with BL-4406.
- 2026-10-31 (interactive, 18:50 Arizona): measured after the fix. Fix 692da47a4, landed 11:11:40 Arizona 2026-10-31. Window: every trace line in `Z:/repos/Curl.logs/DarkFactory-20261031-*-L*.log` from 11:11:40 to 18:35 (shifts 111215 through 182927, 18 lane logs), with a fresh 500-run CI list. The finding's measurement, `Select-String -Pattern '<id> (claim|end|REQUEUE|split)'`, run for every task in the window: one task split, BL-4421 (filed 12:14:22, the CI hang), and only once - one run (12:16:27-12:48:07, 31.6 min), one REQUEUE (12:50:43, cause not named), then one split over its cost cap at 13:22:12 into BL-4427 and BL-4428 (13:24:51); no task split twice, none Deferred after a split (expected: split once, early). That one run committed its tested work before the requeue (`commit ok` 12:47:52, 12:48:00; shared-branch build and fast tests green 12:50:39; pushed 12:50:43), so nothing went through the stash. No other REQUEUE in the window, and no trace line says shift deadline, out of time, stash or uncommitted for a requeue; the other 15 claimed tasks ended Done or Blocked in their one run. Expected result met.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-10-31: Doing -> Done. Measured after 692da47a4: one split (BL-4421) in the window, once, after one run that committed its tested work; no deadline or stash requeue
