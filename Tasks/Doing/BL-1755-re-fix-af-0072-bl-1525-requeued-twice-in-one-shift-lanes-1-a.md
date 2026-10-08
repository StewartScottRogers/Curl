---
id: BL-1755
title: Re-fix AF-0072: BL-1525 requeued twice in one shift (lanes 1 and 4), three claims in its task Log, each run ending short of a timing target with its code left only in the stash
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed:
---
# BL-1755 — Re-fix AF-0072: BL-1525 requeued twice in one shift (lanes 1 and 4), three claims in its task Log, each run ending short of a timing target with its code left only in the stash

## Goal

The defect the audit office reported as AF-0072 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0072 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0072-bl-1525-requeued-twice-in-one-shift-lanes-1-and-4.md`.

Re-fix: the earlier task(s) BL-1691 reached Done, and a later re-audit by the process auditor found the reproduction still reproduces:

- 2026-10-08 | 2026-10-08_0748.md | reproduces: yes | Ran the reproduction: BL-1525 claims 3, outcome done, costUsd 1.7674. Select-String shows L1 claim 11:11:35, stash 11:25:10, REQUEUE 11:25:40 and L4 claim 11:25:58, stash 12:18:27, REQUEUE 12:20:37.

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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
