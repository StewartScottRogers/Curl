---
id: AF-0072
title: BL-1525 requeued twice in one shift (lanes 1 and 4), three claims in its task Log, each run ending short of a timing target with its code left only in the stash
auditor: process
severity: Medium
status: proposed
reason:
key: process:logs:BL-1525:redone-work
reproduction: none
task: none
tasks:
found: 2026-10-07
found-at: 0fcb5afc262ef32bb48ad058cf1f4a2b2c68d511
scorecard: 2026-10-07_1336.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0072 - BL-1525 requeued twice in one shift (lanes 1 and 4), three claims in its task Log, each run ending short of a timing target with its code left only in the stash

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261007-111121-L4.log:108`: BL-1525 requeued twice in one shift (lanes 1 and 4), three claims in its task Log, each run ending short of a timing target with its code left only in the stash.

## Evidence

Location: `logs/DarkFactory-20261007-111121-L4.log:108`

Measure-FactoryProcess.ps1 -Since 2026-10-07: BL-1525 claims 2, outcome requeued, 54.65 min, 5.254 USD (second-costliest task). Lane 1 claimed it 11:11:35, edited Field448.cs/Field25519.cs and passed 1335/1335 tests, but moved it back at 11:24:50 ('L1 BL-1525 stash uncommitted work kept', REQUEUE 11:25:40, 49 turns, 13.5 min, 2.77 USD). Lane 4 claimed it again 18 seconds later (11:25:58), resumed the stash, added Accumulator128.cs and requeued it at 12:20:37 (58 turns, 52.4 min, 2.48 USD). The task Log records three Backlog->Doing / Doing->Backlog pairs on 2026-10-07. Why: the acceptance target is a 5x speed-up measured by benchmark while 8 other lanes load the machine (L1 run text: 'because 8 other lanes are loading the machine'). The measured X25519 gain fell from 4.9x to 3.5x to 2.9x across runs, so each run missed the target and handed uncommitted code to the shift's stash for the next lane. A shared stash holds the work between claims (lost-work risk). Lane 1 also hit 'build FAIL Remove-Item on system path' (L1.log:4).

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1525' | Select-Object id,claims,outcome,costUsd; Select-String -Path ..\logs\DarkFactory-20261007-111121-L1.log,..\logs\DarkFactory-20261007-111121-L4.log -Pattern 'BL-1525 (claim|stash|REQUEUE|DONE)'
```

- Expected: BL-1525 claimed once and DONE, or requeued at most once.
- Actual: BL-1525 claims 2, outcome requeued, 5.254 USD; claim/stash/REQUEUE in L1 (11:11:35-11:25:40) and in L4 (11:25:58-12:20:37).

## Re-audits

## Log

- 2026-10-07: filed proposed.
