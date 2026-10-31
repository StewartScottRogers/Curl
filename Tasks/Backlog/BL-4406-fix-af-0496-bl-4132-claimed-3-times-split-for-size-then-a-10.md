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
completed:
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

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-31 (interactive): measured with Measure-FactoryProcess.ps1 over a snapshot of Z:/repos/Curl.logs (files written since 2026-10-30 12:00; the one live run BL-4420-L1 left out as running) with a fresh 500-run CI list, at about 11:10 Arizona 2026-10-31. Claims hold (tasksClaimedMoreThanOnce 0 since eb3e574f6), but the second half does not: BL-4377's lane-2 run on 10-31 (03:01-03:16) built and tested its change (criteria 1-3, 0 FAIL) and left it uncommitted for the shift to stash, because criterion 4 needed a shift log; the shift then split BL-4377 for cost and BL-4384 recommitted it from the stash. Stays open: the lane rule that hands tested work back to be stashed needs a commit-then-requeue path.

## Log

- 2026-10-31: Created.
