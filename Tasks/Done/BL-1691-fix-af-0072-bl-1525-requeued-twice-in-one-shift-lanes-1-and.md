---
id: BL-1691
title: Fix AF-0072: BL-1525 requeued twice in one shift (lanes 1 and 4), three claims in its task Log, each run ending short of a timing target with its code left only in the stash
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed: 2026-10-07
---
# BL-1691 — Fix AF-0072: BL-1525 requeued twice in one shift (lanes 1 and 4), three claims in its task Log, each run ending short of a timing target with its code left only in the stash

## Goal

The defect the audit office reported as AF-0072 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0072 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0072-bl-1525-requeued-twice-in-one-shift-lanes-1-and-4.md`.

Location: `logs/DarkFactory-20261007-111121-L4.log:108`

Location: `logs/DarkFactory-20261007-111121-L4.log:108`

Measure-FactoryProcess.ps1 -Since 2026-10-07: BL-1525 claims 2, outcome requeued, 54.65 min, 5.254 USD (second-costliest task). Lane 1 claimed it 11:11:35, edited Field448.cs/Field25519.cs and passed 1335/1335 tests, but moved it back at 11:24:50 ('L1 BL-1525 stash uncommitted work kept', REQUEUE 11:25:40, 49 turns, 13.5 min, 2.77 USD). Lane 4 claimed it again 18 seconds later (11:25:58), resumed the stash, added Accumulator128.cs and requeued it at 12:20:37 (58 turns, 52.4 min, 2.48 USD). The task Log records three Backlog->Doing / Doing->Backlog pairs on 2026-10-07. Why: the acceptance target is a 5x speed-up measured by benchmark while 8 other lanes load the machine (L1 run text: 'because 8 other lanes are loading the machine'). The measured X25519 gain fell from 4.9x to 3.5x to 2.9x across runs, so each run missed the target and handed uncommitted code to the shift's stash for the next lane. A shared stash holds the work between claims (lost-work risk). Lane 1 also hit 'build FAIL Remove-Item on system path' (L1.log:4).

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1525' | Select-Object id,claims,outcome,costUsd; Select-String -Path ..\logs\DarkFactory-20261007-111121-L1.log,..\logs\DarkFactory-20261007-111121-L4.log -Pattern 'BL-1525 (claim|stash|REQUEUE|DONE)'
```

- Expected: BL-1525 claimed once and DONE, or requeued at most once.
- Actual: BL-1525 claims 2, outcome requeued, 5.254 USD; claim/stash/REQUEUE in L1 (11:11:35-11:25:40) and in L4 (11:25:58-12:20:37).

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause: each lane skipped only the tasks it had attempted itself (`$attempted`), so a task one lane sent back to Backlog was offered to the next free lane seconds later (BL-1525: L1 requeued 11:25:40, L4 claimed 11:25:58).
- Fix (`RunDarkFactory.ps1`): a lane that ends a task requeued or parked appends its ID to the shift-wide `lanes-<stamp>\requeued.txt` (`Add-ShiftRequeued`), and every lane's claim skips those IDs (`Get-ShiftRequeued`). A task is now requeued at most once per shift; the next shift tries it afresh. The script header says so.
- Default taken: parked tasks count too, and a task requeued for `depends-on` also waits for the next shift even if its blockers finish in this one - a small throughput cost, against repeated stash handoffs between lanes.
- The reproduction reads the 2026-10-07 logs, which will always show that shift's history; its expected result applies to shifts run with this fix, which is what the process auditor's re-audit checks. The helpers were exercised by extracting them from the script's AST: duplicate IDs collapse and the shift's list merges with the lane's own skip list.

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. No lane claims a task another lane of the same shift requeued or parked; requeued at most once per shift
