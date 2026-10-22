---
id: BL-3082
title: Fix AF-0329: BL-2834 requeued twice: a file held by another task's whole-library touches, then a requeue only to wait for CI
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Requeue.ps1, DarkFactory/Prompts.ps1]
lane: no
requirement: none
created: 2026-10-22
completed:
---
# BL-3082 — Fix AF-0329: BL-2834 requeued twice: a file held by another task's whole-library touches, then a requeue only to wait for CI

## Goal

The defect the audit office reported as AF-0329 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0329 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0329-bl-2834-requeued-twice-a-file-held-by-another-task.md`.

Location: `logs/DarkFactory-20261021-064209-L5.log:418`

Location: `logs/DarkFactory-20261021-064209-L5.log:418`

Claim 1 (064209-L5): 13:00:22 to 13:04:07, 25 turns. At 13:05:59 REQUEUE 'Needs Curl.Engine.UnitLibrary/EngineComposition.cs ..., which BL-2921 in Doing holds as the whole Curl.Engine.UnitLibrary'. A whole-project touches on BL-2921 serialised it, which CLAUDE.md forbids ('never a whole project it only adds to'). Claim 2 (150011-L3): 16:58:20 to 17:19:51, 55 turns. At 17:20:48 REQUEUE 'Code committed; only the CI-on-Linux-and-macOS criterion is left'. A third claim, logged only in the task Log, then confirmed CI and moved it to Done. Total cost $4.93. The second requeue exists only because a lane cannot see CI for its own push.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-19 -LogRoot Z:\repos\Curl.audit\20261022-013026\logs -CiRunsJson Z:\repos\Curl.audit\20261022-013026\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2834'; Select-String -Path Z:\repos\Curl.audit\20261022-013026\logs\DarkFactory-*-L*.log -Pattern 'BL-2834 +(claim|REQUEUE|DONE)'
```

- Expected: BL-2834 requeued fewer than twice.
- Actual: claims=2, costUsd=4.9325, 2 REQUEUE lines (13:05:59 outside touches; 17:20:48 waiting on CI).

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The process measurement (`Audit/Tools/Measure-FactoryProcess.ps1 -LogRoot Z:\repos\Curl.logs` with a `-CiRunsJson` built by `gh run list --workflow CI --branch work/dark-factory` (the audit run's own command), counting only lane-trace lines and CI runs at or after the window start), run over the live logs since 2026-10-22 09:54:28 (65f5286ec, rule 11 in both lane prompts), shows no task requeued twice and no REQUEUE whose reason is waiting for CI.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Interactive only: criterion 1 runs a script under the office path a lane may not read. Remove lane: no once an interactive session has run the reproduction (rule 11 in DarkFactory/Prompts.ps1 is already in the tree, build and fast tests green) and ticked criterion 1.

- Requeue.ps1 only moves Blocked tasks back; the two requeues were lane decisions, so the fix is rule 11 in both prompt copies of DarkFactory/Prompts.ps1 (added to touches; no other Doing task holds it). The reproduction script is in an office path a lane may not read, so the re-audit checks criterion 1.

- 2026-10-22 (interactive): Rule 11 had never reached the tree (BL-3082's run left it in stash e1e97e72, now obsolete); 65f5286ec (09:54:28) adds it to both prompt copies. Baseline before it, whole of 2026-10-22 to 09:30: 7 REQUEUE lines, none waiting for CI, no task requeued twice. Left in Backlog: no window after the fix yet; run the measurement again once a shift has run on 65f5286ec.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-22: Doing -> Backlog. Interactive only: the reproduction is under an audit path a lane may not run; rule 11 added to Prompts.ps1 kept uncommitted
