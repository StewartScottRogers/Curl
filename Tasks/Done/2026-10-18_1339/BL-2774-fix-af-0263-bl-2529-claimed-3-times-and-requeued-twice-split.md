---
id: BL-2774
title: Fix AF-0263: BL-2529 claimed 3 times and requeued twice: split for size, then held by a case-runner gap (BL-2601), before finishing
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2774 — Fix AF-0263: BL-2529 claimed 3 times and requeued twice: split for size, then held by a case-runner gap (BL-2601), before finishing

## Goal

The defect the audit office reported as AF-0263 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0263 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0263-bl-2529-claimed-3-times-and-requeued-twice-split-f.md`.

Location: `logs/DarkFactory-20261017-054934-L1.log:26`

Location: `logs/DarkFactory-20261017-054934-L1.log:26`

Measure-FactoryProcess.ps1: BL-2529 claims 3, 9.9 min, $4.20, done. Claim 1 at 03:23:39 L5 (DarkFactory-20261017-021554-L5.log:97), requeued at 03:35:00: 'Split for size: waits on BL-2595 (lib530) and BL-2596 (lib525, lib526, lib533)' (24 turns, $1.31). Claim 2 at 05:49:52 L1 (DarkFactory-20261017-054934-L1.log:2), requeued at 05:59:24: 'Waits on BL-2601: the case runner skips cases 520 to 537 as not ported although the six ports are registered'. Its work went to stash 4ce432d4 (46 turns, $1.93). Claim 3 at 08:27:40 L1 applied stash 4ce432d4 and finished at 08:37:34 (25 turns, $0.96). Two runs ($3.24) ended in requeues because the task was too big and depended on runner work that was not filed when it was claimed. The task's Log agrees with the run logs.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-16 -LogRoot Z:\repos\Curl.audit\20261018-104525\logs -CiRunsJson Z:\repos\Curl.audit\20261018-104525\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-2529'; Select-String -Path Z:\repos\Curl.audit\20261018-104525\logs\DarkFactory-*-L*.log -Pattern 'BL-2529 (claim|REQUEUE|DONE)'
```

- Expected: BL-2529 claimed once and Done without a requeue.
- Actual: claims 3, costUsd 4.2003; REQUEUE 03:35:00 (split for size), REQUEUE 05:59:24 (waits on BL-2601), DONE 08:37:34.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

The lane could not run the reproduction (the audit folder is closed to factory lanes). Fix: the task-run prompt in RunDarkFactory.ps1 (both copies) now tells a run, in its first-pass sizing, to confirm the case runner runs ported upstream cases and to file runner work with the split, so one requeue names it all. BL-2529 history cannot change; the re-audit judges.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-18: Doing -> Done. task-run prompt now makes the first-pass check cover the case runner for upstream-case ports
