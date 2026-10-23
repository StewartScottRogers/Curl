---
id: BL-3144
title: Keep a shift handed over during the merge's CI wait from removing the next shift's ci-watch worktree
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/ShiftCoordinator.ps1, DarkFactory/CiWatch.ps1]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3144 — Keep a shift handed over during the merge's CI wait from removing the next shift's ci-watch worktree

## Goal

When a -Continuous shift starts the next shift during its merge's CI wait, it never cleans or removes the ci-watch worktree the next shift is using.

## Context

Found by the shift-end review of the shift merged at 5a29b06be. The code-reviewer and silent-failure-hunter agents both found it, with medium-high confidence. The hand-over during the merge's CI wait came with BL-3142 (ADR-0758 amendment).

**Where:** `DarkFactory/ShiftCoordinator.ps1`. The `$pollDuringMerge` hand-over (around line 470) starts the next shift through `$startNextShift -Beside`. Afterwards, at lines 488-489, the old coordinator still runs `Invoke-CiWatch -Branch $branch -Final` and `Remove-CiWatch`.

**What is wrong:**

- `$CiWatchDir` is the one fixed path `$LanesDir\ci-watch` (`DarkFactory/CiWatch.ps1:11`), shared across shifts.
- The new coordinator starts using that path on its first heartbeat, about 3 minutes in. The old merge can go on waiting for up to 60 minutes.
- The old coordinator's `-Final` watch then runs `checkout -f` and `clean -fd -- Tasks` in that worktree (`CiWatch.ps1:492-493`). `Remove-CiWatch` (`CiWatch.ps1:635`) runs `git worktree remove --force` on it.
- Either one can delete CI-fix task files the new coordinator has written but not yet pushed. Every git error there goes to `Out-Null`, and the new coordinator may already have marked that failure as settled. So a High CI-fix task can go unfiled with nothing in the trace.
- Before BL-3142 the next shift always started after `Remove-CiWatch`, so the two never overlapped.

## Acceptance criteria

- [x] After a hand-over during the merge's CI wait (`$handOver.Done`), the old coordinator does not run `Remove-CiWatch` on a ci-watch worktree the next shift uses. Either it skips the removal, or each shift has its own ci-watch directory (for example named by `$Stamp`) and removes only its own.
- [x] After such a hand-over, the old coordinator's `-Final` CI watch neither checks out nor cleans a worktree the next shift uses. Each CI failure on the branch is still filed by exactly one of the two coordinators.
- [x] A self-test in `DarkFactory/CiWatch.ps1` or `DarkFactory/ShiftCoordinator.ps1` (run through `RunDarkFactory.ps1`'s test switches) covers the hand-over case and passes.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Chose the skip option: `Invoke-MergeEndCiWatch` (CiWatch.ps1) runs the final watch and `Remove-CiWatch` only when no hand-over happened; after one, the next shift (which watches the same branch from its own loop) is the only filer, so each failure is filed by exactly one coordinator. Self-test: `RunDarkFactory.ps1 -TestCiWatch`, "a handed-over shift runs no final watch and removes no worktree". Build clean, fast tests green.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 1291aedf b2135200 5ee32986 a0b49a29.
- 2026-10-22: Doing -> Done. a shift handed over during the merge's CI wait no longer runs the final watch or removes the next shift's ci-watch worktree
