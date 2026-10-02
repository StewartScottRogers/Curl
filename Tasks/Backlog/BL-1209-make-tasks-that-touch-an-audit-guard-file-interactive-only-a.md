---
id: BL-1209
title: Make tasks that touch an audit guard file interactive only, as the CI audit guard requires
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [.claude/skills/task-board/task-board.ps1, .claude/skills/task-board/SKILL.md]
lane: no
requirement: none
created: 2026-10-02
completed:
---
# BL-1209 — Make tasks that touch an audit guard file interactive only, as the CI audit guard requires

## Goal

A task whose `touches` names an audit guard file is interactive only, like one that touches `Audit/`, so no lane can claim work that the CI audit guard will fail.

## Context

- 2026-10-02: BL-1182 (the Backlog-depth whisper) touched `.claude/skills/task-board/task-board.ps1`. The board offered it to a lane, lane 9 built it on `work/dark-factory`, and every CI run there failed "Audit paths untouched by the dark factory" from 10:09 until PR #48 brought the same file to `master` through the `audit` branch (BL-1194). The shift-end merge to `master` was blocked meanwhile.
- `task-board.ps1`'s `Test-AuditPath` (line ~201) counts only `Audit`, `Audit/...` and `.claude/agents/audit-*` as audit paths. `Audit/Guard/Test-AuditPathsUntouched.ps1`'s `$GuardedPatterns` also guards `.claude/hooks/guard-audit-paths.ps1`, `.claude/settings.json`, `.github/workflows/ci.yml` and `.claude/skills/task-board/task-board.ps1`.
- `Test-AuditPath` drives every refusal and the interactive-only marking in `status`, `next`, `capacity`, `new` and `move` (SKILL.md, "An audit path ..."), so widening it widens all of them.
- This task itself changes a guard file, so it is interactive only (`lane: no`) and reaches `master` through the `audit` branch: do the work there, merge its pull request under the standing exception once CI is green, then merge `master` into `work/dark-factory`. Changing `task-board.ps1` on `work/dark-factory` turns the audit guard red again.
- Keep the two lists from drifting apart. Decided by Claude under Stewart's delegation: `Test-AuditPath` keeps its own list, in the same order as `$GuardedPatterns`, with a comment naming the guard's list. `Test-AuditPathsUntouched.ps1 -SelfTest` gains a case that fails when a guarded path is not an audit path to `task-board.ps1`, so the next guard added without the board fails a self-test, not a shift.

## Acceptance criteria

- [ ] `Test-AuditPath` is true for each of the four guard files above (any letter case) as well as for the three paths it covers today, and false for a neighbour such as `.claude/hooks/whisper-milestone.ps1` or `.github/workflows/gource.yml`.
- [ ] In a scratch board, `status` marks a task touching `.github/workflows/ci.yml` as interactive only, `next` and `capacity` never offer it, and with `CURL_DARK_FACTORY_LANE` set, `new` without `-NoLane` and `move -To Doing` refuse it.
- [ ] `Test-AuditPathsUntouched.ps1 -SelfTest` has a passing case that compares `$GuardedPatterns` with `task-board.ps1`'s `Test-AuditPath`, and it fails when one of the guard files is removed from `Test-AuditPath` (checked by hand, then restored; Notes records it).
- [ ] SKILL.md's paragraph defining an audit path names the guard files.
- [ ] Merged to `master` through the `audit` branch, and the next CI run on `work/dark-factory` passes the audit guard.
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes
## Log

- 2026-10-02: Created.
