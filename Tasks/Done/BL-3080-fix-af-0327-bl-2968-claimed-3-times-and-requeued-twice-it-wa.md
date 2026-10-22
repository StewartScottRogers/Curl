---
id: BL-3080
title: Fix AF-0327: BL-2968 claimed 3 times and requeued twice: it waited on BL-2975, then its criteria were rewritten, and its stash failed to apply on the third claim
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1, DarkFactory/TaskStash.ps1]
lane: no
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3080 — Fix AF-0327: BL-2968 claimed 3 times and requeued twice: it waited on BL-2975, then its criteria were rewritten, and its stash failed to apply on the third claim

## Goal

The defect the audit office reported as AF-0327 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0327 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0327-bl-2968-claimed-3-times-and-requeued-twice-it-wait.md`.

Location: `logs/DarkFactory-20261021-064209-L5.log:434`

Location: `logs/DarkFactory-20261021-064209-L5.log:434`

Claim 1 (064209-L5): 13:06:12 to 13:12:44, 44 turns. Its work was stashed. At 13:13:05 REQUEUE 'Waits on BL-2975: Curl on Windows turns the non-ASCII argument of test472 into ? (ANSI argv model, ADR-0022)'. Claim 2 (150011-L2) at 15:47:20 applied stash f1d42a8c. It ran 31 turns, then at 15:57:12 REQUEUE 'Criteria rewritten: Unicode and test1703/test472 dropped per ADR-0741'. Claim 3 (192752-L4) at 19:31:32: 'stash f1d42a8c from an earlier run did not apply cleanly', so the same stash was offered again after it had already been applied and committed. It ran 10 turns and was DONE at 19:39:06. Cause: the task was claimed while its criteria depended on an undecided behaviour (Unicode argv on Windows). Total cost $3.89 over 3 runs.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-19 -LogRoot Z:\repos\Curl.audit\20261022-013026\logs -CiRunsJson Z:\repos\Curl.audit\20261022-013026\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2968'; Select-String -Path Z:\repos\Curl.audit\20261022-013026\logs\DarkFactory-*-L*.log -Pattern 'BL-2968 +(claim|stash|REQUEUE|DONE)'
```

- Expected: BL-2968 claimed fewer than 3 times and requeued fewer than twice.
- Actual: claims=3, costUsd=3.8855, outcome=done; 2 REQUEUE lines; stash f1d42a8c applied at 15:47:20 and then 'did not apply cleanly' at 19:31:33.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The process measurement (`Audit/Tools/Measure-FactoryProcess.ps1 -LogRoot Z:\repos\Curl.logs` with a `-CiRunsJson` built by `gh run list --workflow CI --branch work/dark-factory` (the audit run's own command), counting only lane-trace lines and CI runs at or after the window start), run over the live logs since 2026-10-22 08:07:46 (shift 20261022-080746, the first to run 0480ede18, which drops a stash once applied), shows no task claimed 3 or more times or requeued twice, and no stash offered again after it was applied (no `did not apply cleanly` line).
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Cause: Restore-TaskStash applied a stash but left it in the list, so the next claim was offered the same, already committed, stash. Fix: DarkFactory/TaskStash.ps1 drops the stash once applied; -TestTaskStash pins it. Added DarkFactory/TaskStash.ps1 to touches (no other Doing task names it). The reproduction reads past logs through the audit office tools, which a lane may not run; the re-audit confirms the fix.

- 2026-10-22 (interactive): Live window 2026-10-22 08:07:46 to 09:30:18: 13 claims, none of a task 3 or more times, 1 REQUEUE trace line (BL-3099 at 08:33:02, a trace of a task that reached Done), 0 `did not apply cleanly` lines. Whole of 2026-10-22: 0 `did not apply cleanly` lines, no task claimed 3 times. `RunDarkFactory.ps1 -TestTaskStash`: 44 PASS, 0 FAIL, including `an applied stash is dropped from the list`. Fix: 0480ede18 (06:58), refined by d0796ba22 (BL-3103, drops only the stash it applied).

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-22: Doing -> Blocked. Stewart: the fix is committed (applied stash is dropped), but the reproduction needs the audit tools a lane may not run; run it interactively and move to Done
- 2026-10-22: Blocked -> Backlog. Criterion 1 reworded to the live-window measurement (process findings about past behaviour are measured on logs written after the fix)
- 2026-10-22: Backlog -> Doing. Criteria at claim: 42b3564e 9742157d.
- 2026-10-22: Doing -> Done. Live logs since 2026-10-22 08:07:46: no task claimed 3 times or requeued twice, 0 stashes that did not apply
