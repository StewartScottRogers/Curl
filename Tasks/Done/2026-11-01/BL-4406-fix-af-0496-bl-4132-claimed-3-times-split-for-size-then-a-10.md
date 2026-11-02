---
id: BL-4406
title: Fix AF-0496: BL-4132 claimed 3 times: split for size, then a 105-minute run that ran out of time with its fix stashed
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
# BL-4406 — Fix AF-0496: BL-4132 claimed 3 times: split for size, then a 105-minute run that ran out of time with its fix stashed

## Goal

The cause of the factory behaviour the audit office reported as AF-0496 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0496 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0496-bl-4132-claimed-3-times-split-for-size-then-a-105.md`.

Location: `logs/DarkFactory-20261030-060635-L4.log`

Location: `logs/DarkFactory-20261030-060635-L4.log`

L11 claimed it at 18:18:09 on 10-29 and requeued it at 18:54:56 ('Split for size: waits on BL-4302, BL-4300, BL-4301'). L4 claimed it at 06:24:02 on 10-30. That run ended after 70 turns and 105.2 min, and was requeued at 08:10:29: 'Out of time: hook and option built and fixed in the stash; needs one CompressionDictionary-only quality run, the fast tests and the commit'. Its result record says 'I ran out of time ... left uncommitted for the shift to stash'. L2 claimed it a third time at 08:11:06, finished in 15 turns and 13.5 min, and was Done at 08:32:18. The 105-min run did nearly all the work but could not commit it, so a third claim, against ADR-1155, finished it.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Z:/repos/Curl.audit/20261031-035829/logs/DarkFactory-*-L*.log -Pattern 'BL-4132 (claim|end|REQUEUE|DONE)'
```

- Expected: At most two claims; a run that has built and tested the change commits it rather than stashing it.
- Actual: 3 claims; REQUEUE 'Split for size' then 'Out of time ... in the stash' after 105.2 min.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-31 (interactive): fix 692da47a4 pushed 11:11 Arizona. Both task prompts now commit a Backlog or Blocked run's code change that is complete in itself (build clean, fast tests green, covered by its own tests) instead of leaving it to the stash, and Invoke-Integrate now runs the build and fast tests on any lane commits that change more than task files whatever the task's state (before, a requeued run's commits were pushed unchecked; red ones are now parked, with no repair run). Self-tests 0 FAIL under both shells (-TestIntegrateLock 19, -TestTaskStash 83, -TestLaneFlags 26). Waits for a shift on the fix before its measurement can be taken.
- 2026-10-31 (interactive): measured with Measure-FactoryProcess.ps1 over a snapshot of Z:/repos/Curl.logs (files written since 2026-10-30 12:00; the one live run BL-4420-L1 left out as running) with a fresh 500-run CI list, at about 11:10 Arizona 2026-10-31. Claims hold (tasksClaimedMoreThanOnce 0 since eb3e574f6), but the second half does not: BL-4377's lane-2 run on 10-31 (03:01-03:16) built and tested its change (criteria 1-3, 0 FAIL) and left it uncommitted for the shift to stash, because criterion 4 needed a shift log; the shift then split BL-4377 for cost and BL-4384 recommitted it from the stash. Stays open: the lane rule that hands tested work back to be stashed needs a commit-then-requeue path.
- 2026-10-31 (interactive, 18:45 Arizona): measured after the fix. Fix 692da47a4, landed 11:11:40 Arizona 2026-10-31. Window: every trace line in `Z:/repos/Curl.logs/DarkFactory-20261031-*-L*.log` from 11:11:40 to 18:35 (shifts 111215, 121444, 132053, 150730, 152043, 175358, 180251, 182927; 18 lane logs), with a fresh 500-run CI list (`gh run list --workflow CI --branch work/dark-factory ... --limit 500`). The finding's measurement, `Select-String -Pattern '<id> (claim|end|REQUEUE|DONE)'`, run for every task in the window: 16 tasks claimed (BL-4421, 4422, 4423, 4424, 4425, 4426, 4427, 4429, 4431, 4432, 4433, 4434, 4435, 4436, 4437, 4442, plus BL-4443 and BL-4444 claimed at 18:34 and still running), each claimed exactly once - none twice, none three times (expected: at most two). One REQUEUE in the window, BL-4421 at 12:50:43 ('Cause not yet named: no local repro'): that run committed its tested change before the requeue (`commit ok` 12:47:52 and 12:48:00), the shift verified it ('build and fast tests green on the shared branch' 12:50:39) and pushed it (12:50:43) - the commit-then-requeue path the fix added, rather than a stash. No trace line in the window says stash, uncommitted or out of time for a requeue; the one 'uncommitted work' line (18:29:36, BL-4442) is a shift stopped from outside at 18:06 whose lane resumed the task in place and finished it in 2.1 min (Done 18:33:44). Expected result met: at most two claims, and a run that built and tested its change committed it.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
- 2026-10-31: Doing -> Done. Measured after 692da47a4: 16 tasks claimed once each since 11:11, and the one requeue (BL-4421) committed its tested change instead of stashing it
