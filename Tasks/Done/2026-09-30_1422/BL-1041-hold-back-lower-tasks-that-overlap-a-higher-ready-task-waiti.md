---
id: BL-1041
title: Hold back lower tasks that overlap a higher ready task waiting on Doing, so a large task like BL-717 is not starved
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [.claude/skills/task-board]
lane: no
requirement: none
created: 2026-09-30
completed: 2026-09-30
---
# BL-1041 — Hold back lower tasks that overlap a higher ready task waiting on Doing, so a large task like BL-717 is not starved

## Goal

A ready task that waits only because its `touches` overlap work in `Doing` is not overtaken by lower tasks on the same projects, so it starts as soon as that work ends.

## Context

On 2026-09-30 BL-717 (Http, Networking, Console) was first in the queue for over an hour and never started: each time a task on those projects finished, a lane claimed a smaller one on the same projects (BL-636 after BL-615). Stewart chose to hold back the rivals rather than run BL-717 interactively or wait.

## Acceptance criteria

- [x] `next` holds back a ready task that overlaps a higher ready task waiting on `Doing`, and offers the first task that is neither waiting nor held back.
- [x] `capacity` counts only tasks `next` would let start, so `-Lanes Auto` sizes to the same rule.
- [x] `status` marks a held-back task `held back behind BL-###`.
- [x] A task given to `-Skip` reserves nothing.
- [x] Once `Doing` frees the projects, the waiting task is offered first.
- [x] `SKILL.md` describes the rule.

## Notes

- Built on branch `audit-hold-back-rivals` from master (task-board.ps1 is a guarded path, BL-998), commit 31bd3700, pull request https://github.com/StewartScottRogers/Curl/pull/29. New `Get-StartOrder` walks the ready queue in `next` order; a task colliding with `Doing` is waiting and reserves its touches; a later task overlapping a reservation is held back.
- Scratch board (Doing BL-001 Console; Backlog BL-002 High Console+Http, BL-003 Http, BL-004 Ssh, BL-005 Console.UnitTests): 9 PASS, 0 FAIL - next offers BL-004 not BL-003; capacity 3 (BL-004, BL-005); status flags; -Skip BL-002 offers BL-003; with nothing free, the message names BL-002 with BL-001 and 1 held back; after BL-001 finishes, next offers BL-002; ASCII only.
- Real board with the new script: `No task can start yet: ... BL-717 with BL-636.`, so lanes wait for BL-636 and BL-717 starts next.
- RunDarkFactory.ps1 reads only IDs from `next`, so the reworded message changes nothing there. Trade-off: while a large task waits, fewer lanes have work and -Lanes Auto retires the idle ones.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. A task waiting on Doing now holds back lower tasks on the same projects; in PR #29, awaiting Stewart's merge.
