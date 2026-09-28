---
id: BL-764
title: Add a capacity command to the task board that counts how many tasks can run at once
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [.claude/skills/task-board]
requirement: none
created: 2026-09-28
completed:
---
# BL-764 — Add a capacity command to the task board that counts how many tasks can run at once

## Goal

`task-board.ps1 capacity` prints how many tasks the board could have running at once right now: the tasks in Doing, plus the ready tasks that could start beside them without any two overlapping in `touches`. The dark factory's `-Lanes Auto` (BL-768) caps its lane count with that number.

## Context

- This is part of the `-Lanes Auto` design Stewart approved on 2026-09-28. Its ceiling rule is item 4 of BL-763's ADR. This task does not wait for that ADR, because everything it needs is written here.
- **Where the code goes.** `.claude/skills/task-board/task-board.ps1`:
  - `[ValidateSet(...)] $Command` near line 48;
  - `Get-ReadyTasks` near line 250, which gives ready tasks in `next` order;
  - `Test-Overlap` near line 175;
  - `Get-Collision` near line 263;
  - the `'next'` case near line 326.

  A task whose `touches` is empty is read as `*` and overlaps everything (`ConvertTo-Task`).
- **The count is greedy, in `next` order.** Start from the touches of every task in Doing. Walk `Get-ReadyTasks`. A task is startable when its touches overlap neither a Doing task nor a task already picked; pick it and add its touches. The answer is Doing count plus picked count.
- **Output: exactly one line**, parseable with `^Capacity (\d+):`:
  - `Capacity 7: 3 in Doing, 4 more can start (BL-499, BL-503, BL-504, BL-506).`
  - `Capacity 3: 3 in Doing, none more can start.`
  - `Capacity 0: 0 in Doing, none more can start.` when nothing is ready or in progress.
- **The script's rules.** Keep it ASCII only (its header explains why). Windows PowerShell 5.1 compatible. `$ErrorActionPreference = 'Stop'` as today.
- **Testing without the live board.** The script finds the board through `$env:CLAUDE_PROJECT_DIR` (near line 93), so it can be pointed at a temporary folder holding `Tasks\Backlog`, `Tasks\Doing` and `Tasks\Done` with hand-made `BL-*.md` files. Build such a fixture under `$env:TEMP` in the check below. Never test by editing the real `Tasks\` folders: a dark factory shift is running and claims from them.
- PowerShell only, no Python.

## Acceptance criteria

- [ ] `capacity` is in the `$Command` `ValidateSet`, in the script's header comment Commands list, and in `SKILL.md`'s command table, with the one-line meaning above.
- [ ] A throwaway fixture board built under `$env:TEMP` gives the expected output when run with `$env:CLAUDE_PROJECT_DIR` set to it. The fixture has:
  - Doing: `BL-001` touching `A`.
  - Backlog, all `assignee: Claude` and `priority: High` (so `next` order is by ID), plus `BL-007` assigned to `Stewart`:
    - `BL-002` touches `A/x.cs`, which overlaps Doing;
    - `BL-003` touches `B`;
    - `BL-004` touches `B/y.cs`, which overlaps the earlier pick `BL-003`;
    - `BL-005` touches `C`;
    - `BL-006` has `depends-on: [BL-007]`, so it is not ready, and `BL-007` is Stewart's, so it is not ready either.

  `capacity` then prints exactly `Capacity 3: 1 in Doing, 2 more can start (BL-003, BL-005).`
- [ ] The same fixture with Doing emptied and `BL-002`'s touches removed (so it reads `*`) prints `Capacity 1: 0 in Doing, 1 more can start (BL-002).`
- [ ] `next` and `status` print the same as before the change on the real board. Compare their output before and after the edit; the live shift may move tasks between the two runs, so compare on the fixture if the board changed.
- [ ] `[System.Management.Automation.Language.Parser]::ParseFile` reports no errors for `task-board.ps1`, and `Select-String -Path .claude\skills\task-board\task-board.ps1 -Pattern '[^\x00-\x7F]'` finds nothing.
- [ ] `git diff --stat` shows only files under `.claude/skills/task-board` changed outside `Tasks/`.

## Notes

## Log

- 2026-09-28: Created.
