---
id: BL-1769
title: Fix AF-0091: BL-1609 claimed 5 times and requeued 4 times in shift 20261007-201432; one run was wasted because lanes may not run git stash list
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1769 — Fix AF-0091: BL-1609 claimed 5 times and requeued 4 times in shift 20261007-201432; one run was wasted because lanes may not run git stash list

## Goal

The defect the audit office reported as AF-0091 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0091 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0091-bl-1609-claimed-5-times-and-requeued-4-times-in-sh.md`.

Location: `logs/DarkFactory-20261007-201432-L9.log`

Location: `logs/DarkFactory-20261007-201432-L9.log`

Claims: L4 21:44:52 (requeued after 1.6 min: 'Unstarted: run budget too small'), L8 21:47:08 (requeued: 'CONNECT measured'), L6 21:52:38 (6.9 min, stash, requeued: 'Code and tests written, uncommitted (shift stash)'), L9 22:00:25 (0.9 min, requeued: 'Unstarted: lane 9 found a clean worktree and no access to the earlier lane's shift stash'), L1 22:19:28 (Done 22:34:38). The run logs BL-1609-20261007-201432-L9.jsonl and -L1.jsonl both show 'Permission to use Bash with command ... git stash list --format='%H %gs' has been denied' (permission_decision reject, source config). L1 got around it with git log -g refs/stash, found stash a9c150b6 ('darkfactory BL-1609 20261007-201432') and applied it, so the work was not lost, but the L9 run and part of L1 went on the refusal. Cost: $0.41+$1.15+$2.03+$0.41+$1.18 = $5.17.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-07 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-1609' | Select-Object id,claims,outcome,costUsd; Select-String -Path ..\logs\DarkFactory-20261007-201432-L*.log -Pattern 'BL-1609 (claim|stash|REQUEUE|DONE)'; Select-String -Path ..\logs\BL-1609-20261007-201432-L9.jsonl -Pattern 'git stash list[^\"]*has been denied' -List
```

- Expected: BL-1609 claimed once or twice; no stash command denied.
- Actual: BL-1609 claims 5, outcome done, costUsd 5.1715; 4 REQUEUE lines; L9 run log shows git stash list denied

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded. (See Notes: the cause is removed; the 2026-10-07 logs it reads are history, so the process auditor's re-audit on later shifts is the proof.)
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause: a lane run gets `git stash` as a hard `--disallowedTools` prefix deny, which also refuses `git stash list`, so a run that claimed a task that had come back with work in the shift stash could not find that work (BL-1609's L9 run, and part of L1's).
- Fix (`RunDarkFactory.ps1`): the shift applies the work itself. On a fresh claim (not a resumed or overtime run, whose work is already in place) `Restore-TaskStash` finds the newest stash named `darkfactory <id> `, applies it with `git stash apply` in the clean worktree and puts a STASHED WORK note in front of the run's prompt. If it does not apply cleanly the worktree is put back clean and the note gives the stash hash with `git diff <sha>^1 <sha>` and `git show <sha>^3:<path>`, which a lane may run. Rule 6 of both prompts now says the stash comes back on the next claim.
- Choice: `git stash` stays denied for runs rather than allowing `git stash list`. A prefix rule cannot allow `list` and still refuse a bare `git stash`, and with the stash stack shared by every worktree, the shift owning it keeps lanes from popping each other's. The applied stash is left in the list, not dropped: dropping by index races other lanes.
- `-TestTaskStash` proves it on a throwaway repository: no stash gives no note, another task's stash is left alone, the task's own is applied (tracked change and new file), and one that conflicts leaves the worktree clean with the hash in the note. `-TestShiftBranch` still passes.
- Reproduction: its `-Since 2026-10-07` run reads logs that cannot change, so it was not rerun here (lanes may not read the audit office's tools); the re-audit judges the fix on shifts after this one.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. A task claimed again after a requeue now starts from its shift-stashed work: the shift applies the stash and tells the run (AF-0091)
