---
id: BL-993
title: Add a lane: no front-matter field that keeps dark factory lanes off a task
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [.claude/skills/task-board, .claude/commands/task-run.md]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-993 — Add a lane: no front-matter field that keeps dark factory lanes off a task

## Goal

A task whose front matter says `lane: no` is never offered by `task-board.ps1 next` or counted by `capacity`, so no dark factory lane ever claims it, while an interactive session still runs it with `/task-run BL-###`.

## Context

Stewart's audit office (planned 2026-09-29; the ADR is BL-994) is built by interactive
Claude sessions, not by lanes: the factory must not write the guards and auditors that
audit it. Today the board has no way to say that. `assignee: Stewart` keeps every Claude
off a task (`move -To Doing` refuses it), which is too strong; pipelines do not select
runners. The dark factory claims only through `next`: `Invoke-Claim` in
`RunDarkFactory.ps1` runs `task-board.ps1 next` and claims what it prints, and `-Lanes
Auto` sizes itself with `capacity`. So skipping `lane: no` in those two commands keeps
every lane off such a task without touching `RunDarkFactory.ps1`.

The tasks filed with this one (BL-995 onward, except BL-1003) already carry `lane: no`,
written by hand, and every one of them depends on this task, directly or through others,
so none can be offered before this lands.

Design, in `.claude/skills/task-board/task-board.ps1`:

- `ConvertTo-Task` reads an optional `lane` field: `no` (case-insensitive) means
  interactive only; absent, empty or `yes` means any runner. Add it to the task object as
  `LaneAllowed` (bool).
- `Get-ReadyTasks` is unchanged (so `status` and `task-admin` still see the task as ready);
  `next` and `capacity` drop tasks whose `LaneAllowed` is false.
- `status` marks such a ready task `[ready, interactive only]` with no queue number, and
  the queue numbers of the others count only lane-eligible tasks.
- `new -NoLane` writes `lane: no` into the front matter (after `touches`).
- `move -To Doing` still accepts a `lane: no` task: that is how an interactive session
  claims it. (Refusing lanes' claims needs a lane marker, which BL-995 adds and BL-996
  uses.)
- `task-admin.ps1 check` reports a `lane` value other than `yes` or `no`; `list` and
  `show` mark interactive-only tasks.
- `.claude/commands/task-run.md`, step 1: with no argument `next` never picks an
  interactive-only task, so name it: `/task-run BL-###`.

Keep `task-board.ps1` ASCII only (its header says why). Check with a scratch board: the
script reads `$env:CLAUDE_PROJECT_DIR` as the repository root, so a temporary folder with
`Tasks\Backlog`, `Tasks\Doing`, `Tasks\Done` and a few hand-written task files tests
every command without touching the real board.

## Acceptance criteria

- [x] On a scratch board holding ready tasks BL-001 (`lane: no`, High) and BL-002 (no `lane` field, Normal), `next` prints a line starting `BL-002` and `capacity` prints `Capacity 1: 0 in Doing, 1 more can start (BL-002).`
- [x] On the same board, `status` shows BL-001 with `[ready, interactive only]` and BL-002 with `[ready, #1 in queue]`.
- [x] On the same board, `move -Id BL-001 -To Doing` succeeds.
- [x] `new -Title "x" -NoLane` writes a file whose front matter contains the line `lane: no`; without `-NoLane` the file has no `lane` line.
- [x] `task-admin.ps1 check` reports a task with `lane: maybe` and reports nothing for `lane: no` or `lane: yes`.
- [x] `SKILL.md`'s front-matter table documents `lane` (values `yes`, `no`; absent means `yes`), `new`'s options list `-NoLane`, and `ADMIN.md` mentions the new `check` rule.
- [x] `.claude/commands/task-run.md` step 1 says an interactive-only task must be named by ID.
- [x] `task-board.ps1` and `task-admin.ps1` contain only ASCII characters.
- [x] Run against the real board, `task-board.ps1 next` never prints any of BL-995 to BL-1002 or BL-1004 to BL-1022.

## Notes

- Both scripts gained `Get-LaneReadyTasks` (`Get-ReadyTasks` minus `lane: no`); `next`, `capacity`, `status` queue numbers and `task-admin list`/`show` queue numbers use it, while `Get-ReadyTasks` itself is unchanged.
- `task-admin show` prints a `lane` row and `ready yes, interactive only` for such a task.
- `check` accepts an absent or empty `lane` (both mean `yes`); only a non-empty value other than `yes`/`no` (case-insensitive) is reported.
- Verified on a scratch board under `%TEMP%` via `CLAUDE_PROJECT_DIR`: every scratch-board criterion passed as written. On the real board a `next -Skip` sweep offered 12 tasks (BL-994, BL-992, BL-750, ...) and none of BL-995..BL-1002 or BL-1004..BL-1022; all of those carry `lane: no`.
- Seen in passing, not touched: `next` warns of a duplicate BL-806 (one live in Backlog, one archived in Done\2026-09-28_1849).

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. A task with lane: no is never offered by next or counted by capacity; interactive sessions still run it by ID
