---
id: BL-4407
title: Fix AF-0497: BL-4137 claimed 3 times: split for size, then a 106-minute, 83-turn run that ran out of time with its code stashed
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
# BL-4407 — Fix AF-0497: BL-4137 claimed 3 times: split for size, then a 106-minute, 83-turn run that ran out of time with its code stashed

## Goal

The cause of the factory behaviour the audit office reported as AF-0497 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0497 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0497-bl-4137-claimed-3-times-split-for-size-then-a-106.md`.

Location: `logs/DarkFactory-20261030-060635-L6.log`

Location: `logs/DarkFactory-20261030-060635-L6.log`

L5 claimed it at 19:26:31 on 10-29 and requeued it at 20:00:08 ('Split for size into BL-4311, BL-4309 and BL-4310'). L6 claimed it at 06:25:08 on 10-30. That run (BL-4137-20261030-060635-L6.jsonl) took 83 turns, $3.91 and 106 min, and was requeued at 08:11:56: 'Out of time: code and tests written (uncommitted, stashed)'. L4 claimed it a third time at 08:13:00, finished in 35 turns, 26.6 min and $0.99, and was Done at 08:44:30. Total $5.65. The same 08:09-08:11 'out of time' requeue hit BL-4131 (L13) and BL-4132 (L4) as well: all three runs were about 105 min long.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Z:/repos/Curl.audit/20261031-035829/logs/DarkFactory-*-L*.log -Pattern 'BL-4137 (claim|end|REQUEUE|DONE)'
```

- Expected: At most two claims.
- Actual: 3 claims, 2 REQUEUE (split for size; out of time after 105.9 min with the code stashed).

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-31: Created.
