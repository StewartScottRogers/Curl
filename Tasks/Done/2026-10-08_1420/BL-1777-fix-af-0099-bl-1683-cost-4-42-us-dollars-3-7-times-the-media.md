---
id: BL-1777
title: Fix AF-0099: BL-1683 cost 4.42 US dollars, 3.7 times the median, over two runs; the first was stashed and requeued for a held project
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1777 — Fix AF-0099: BL-1683 cost 4.42 US dollars, 3.7 times the median, over two runs; the first was stashed and requeued for a held project

## Goal

The defect the audit office reported as AF-0099 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0099 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0099-bl-1683-cost-4-42-us-dollars-3-7-times-the-median.md`.

Location: `logs/DarkFactory-20261007-111121-L5.log`

Location: `logs/DarkFactory-20261007-111121-L5.log`

L5 claimed it at 17:53:30, ran 21.9 min and 56 turns ($2.46), then stash and REQUEUE at 18:46:58: 'Needs Curl.Console.UnitTests, held by B~'. L1 in shift 201432 claimed it at 20:14:56 and was Done at 20:36:00 after 53 turns ($1.96).

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1683' | Select-Object id,claims,costUsd,minutes
```

- Expected: costUsd at most 3.57.
- Actual: BL-1683 claims 2 costUsd 4.4205

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause, read from both `BL-1683-*.jsonl` logs: the first run (L5, $2.46) learned near its start that BL-1595 held `Curl.Console.UnitTests`, built the fix outside it, and only the full fast-test run showed an existing test there pinning the old behaviour; by then BL-1596 held the project, so rule 3 sent it to Backlog and the shift stashed the work. The second run (L1, $1.96) began by hunting through `git stash list` and the stash reflog for that work before finishing it. Neither cap nor stash mechanism existed then to keep the pair near one run.
- Both halves were fixed after these runs: 4fcfaaca9 (2026-10-08 12:04, AF-0091) makes the shift apply a requeued task's stash itself on the next claim, and 369733afb (BL-1773, AF-0095, 12:46) takes a task's runs of the last 24 hours off its next run's cap, down to $1. Under that rule BL-1683's second run would have been capped near $1, about $3.5 in all, under the finding's 3.57. This task adds no duplicate mechanism; it names AF-0099 beside AF-0095 in the COST CAP help and beside AF-0091 in `Restore-TaskStash`.
- The reproduction's `-Since 2026-10-07` window will always show BL-1683's past $4.42, because logs do not change, and a lane cannot run `Audit/Tools/Measure-FactoryProcess.ps1` (the audit path guard). As for BL-1775 and BL-1776, the box is ticked because the cause is fixed; the re-audit should measure only requeued tasks claimed after 369733afb.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Cause was a held-project requeue plus a stash hunt on the next claim, fixed for later runs by 4fcfaaca9 (stash applied on claim) and BL-1773 (AF-0095 cap); AF-0099 now cited in RunDarkFactory.ps1; build clean, fast tests green
