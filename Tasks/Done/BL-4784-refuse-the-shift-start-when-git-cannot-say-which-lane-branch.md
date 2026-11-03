---
id: BL-4784
title: Refuse the shift start when git cannot say which lane branches are ahead of an orphan
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/ShiftCoordinator.ps1, DarkFactory/Park.ps1]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4784 — Refuse the shift start when git cannot say which lane branches are ahead of an orphan

## Goal

When git cannot say which lane branches are ahead, a shift start does not start lanes, which would reset branches that may hold an orphan's only copy of its work. Only the real "cannot tell which lane" case is Blocked and carried on.

## Context

Found by the shift-end review of 2026-11-03 (silent-failure-hunter, Important), in the shift merged at 7410f102c (BL-4715, ADR-1234).

- `DarkFactory/ShiftCoordinator.ps1` ~line 274 now turns every `refuse` from `Get-OrphanAction` into a move to Blocked, and the shift goes on.
- `Get-LaneBranchesAhead` (`DarkFactory/Park.ps1:117-129`) returns the `$UnknownLaneBranches` placeholder when git fails. Its comment says this is so `Get-OrphanAction` "refuses rather than requeues".
- With this change, that placeholder only leads to Blocked. `Get-OrphanBlockReason` copies only refs that match `^factory/lane-(\d+)$`, so it keeps nothing and does not say the branches went unchecked.
- The lanes then start and reset their branches, so any unpushed lane commits survive only in the reflog.

## Acceptance criteria

- [x] When `$laneBranchesAhead -contains $UnknownLaneBranches`, the shift start stops with a message that names the failed git query, as it did before BL-4715.
- [x] A refusal with known lane branches is still Blocked, with its candidates kept, and the shift carries on.
- [x] A factory self-test (`RunDarkFactory.ps1 -Test*`) covers both cases.
- [x] ADR-1234 states the exception.

## Notes

Get-UnknownLaneBranchesRefusal (Park.ps1) is checked in ShiftCoordinator before the orphan loop; -TestPark covers unknown and known cases. Known-branch Blocked path already covered by the AF-0567 test.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 0e4829b5 b353cccc 9fb14368 3e851acc.
- 2026-11-03: Doing -> Done. Shift start stops naming the failed git query when lane branches are unknown; -TestPark covers both cases; ADR-1234 states the exception
