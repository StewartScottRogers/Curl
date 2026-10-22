---
id: BL-3060
title: Retry a lane push lost to a board-only push without re-verifying or spending an attempt
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Integrate.ps1, DarkFactory/ShiftEnd.ps1, DarkFactory/ShiftCoordinator.ps1]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3060 — Retry a lane push lost to a board-only push without re-verifying or spending an attempt

## Goal

A lane whose verified work loses a push race to a push that changes nothing its tests cover (a board-only push, a shift-end review push) pushes again without re-running tests and without spending one of its 3 attempts, and the coordinator's own pushes take the integrate lock so they stop causing the race.

## Context

On 2026-10-21 between 20:53 and 20:56 Arizona, lane 1 parked BL-3011 after 3 non-fast-forward push refusals. During each re-verify of about 50 s under the integrate lock, a push outside the lock landed on work/dark-factory: an interactive session's commits 4aa788026, 0128f30eb and f8fc65f3f, or the ending shift's review push 979adf0e4 ("file shift-end review findings"). `Invoke-Integrate` in `DarkFactory/Integrate.ps1` counts each lost race as one of its 3 attempts (`while ($attempt -lt 3)`) and then rebases, dedupes, archives and re-verifies with `Test-Green` in full.

Fix in two parts:
1. In `Invoke-Integrate`, when `git push` is refused after a green verify, fetch and rebase again. If the rebase is clean and the incoming commits (the diff between the old and new `origin/$Branch`) touch nothing the lane's test selection covers (use the selection logic in `DarkFactory/AffectedTests.ps1`; at minimum, only `Tasks/` files), push again without `Test-Green` and without incrementing `$attempt`. Bound this fast retry (for example 10 times) so it cannot loop for ever. Any incoming change that the selection covers, a conflict, or a failed fetch keeps today's path (re-verify, spends an attempt).
2. The coordinator's own pushes to the branch take the integrate lock (`Enter-Lock`): the shift-end review push (prompt text in `DarkFactory/ShiftEnd.ps1`, line 87 area, which tells the reviewer to push; either have the coordinator do the push under the lock or have the prompt's push step take it) and the board-move push in `DarkFactory/ShiftCoordinator.ps1` (line 53 area). Name in Notes anything that cannot take the lock and why.

The existing self-test is `RunDarkFactory.ps1 -TestIntegrateLock` (body in `DarkFactory/Integrate.ps1`: a throwaway repo with a bare origin and a second clone standing in for other lanes). Extend it; do not edit `.claude/skills/task-board/task-board.ps1` or other guard files.

## Acceptance criteria

- [x] `-TestIntegrateLock` has a case where a Tasks/-only commit is pushed from the other clone during the lane's verify; the lane's push then succeeds, the stand-in verify ran once, and the attempt count is unchanged.
- [x] `-TestIntegrateLock` has a case where the incoming commit changes a file the test selection covers; the lane re-verifies and spends an attempt, as before.
- [x] `-TestIntegrateLock` has a case where three covered-file races in a row still end with "push kept being refused", so the 3-attempt limit stands.
- [x] The coordinator's shift-end review push and its board-move push are made while holding the integrate lock, or Notes states why one cannot; a self-test or a text check in the self-tests covers each.
- [x] `RunDarkFactory.ps1 -TestIntegrateLock` and the other `-Test*` self-tests that exist for the touched files pass, and `RunDarkFactory.ps1` still parses.

## Notes

- Decided by Claude: "covered by the test selection" is taken at its stated minimum: an incoming change is board-only when every changed path starts with `Tasks/`; anything else re-verifies and spends an attempt as before. The fast retry is bounded at 10 tries (`Invoke-PushRetryWithoutVerify`) and runs the dedupe/archive housekeeping (now `Update-BoardBeforePush`) before each re-push.
- Shift-end review: the reviewer prompt no longer pushes; `Push-ReviewCommitsUnderLock` in ShiftEnd.ps1 fetches, rebases and pushes its commit while holding `Enter-Lock`, after the run ends. The reviewer's own dedupe still runs before that, so an ID taken by a lane meanwhile is not renumbered at push time.
- Coordinator: the board-move push (return of orphans to Backlog) takes `Enter-Lock`. The coordinator has no other push to the branch.
- Self-test: `-TestIntegrateLock` redefines `Test-Green` as a counting stand-in that pushes from the other clone; the two lock checks are text checks of ShiftEnd.ps1 and ShiftCoordinator.ps1.
## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: fdb4f146 4e309858 9f16808d d4846cb4 270ab631.
- 2026-10-22: Doing -> Done. A lane push lost to a Tasks-only push is retried without re-verifying or spending an attempt; coordinator and review pushes take the integrate lock
