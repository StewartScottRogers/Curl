---
id: BL-996
title: Refuse audit-path tasks and interactive-only claims from dark factory lanes in task-board.ps1
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-993, BL-995]
touches: [.claude/skills/task-board]
lane: no
requirement: none
created: 2026-09-29
completed:
---
# BL-996 — Refuse audit-path tasks and interactive-only claims from dark factory lanes in task-board.ps1

## Goal

When `CURL_DARK_FACTORY_LANE` is set, `task-board.ps1` refuses to file a lane-claimable task whose `touches` name an audit path and refuses to claim any interactive-only task, and `next`/`capacity` never offer a task that touches an audit path.

## Context

Interactive only (`lane: no`): audit guard layer 1 (the ADR from BL-994). Run it with
`/task-run BL-996`.

Audit paths are `Audit/` and `.claude/agents/audit-*`. After BL-993 a task can say
`lane: no`, and `next`/`capacity` skip it; after BL-995 every factory process carries
`CURL_DARK_FACTORY_LANE`. This task makes the board enforce the rule rather than rely on
every task being marked.

Design, in `.claude/skills/task-board/task-board.ps1`:

- `Test-AuditPath([string] $TouchPath)`: true when the normalised touch path (the output
  of `ConvertTo-TouchPath`) equals `Audit` or starts with `Audit/`, or matches
  `.claude/agents/audit-*` with `-like`, all case-insensitively. An ancestor such as
  `.claude` or `*` is not an audit path here: the hook (BL-997) and CI (BL-998) catch
  real writes, and refusing `*` would refuse every task filed without `touches`.
- A task is interactive only when it says `lane: no` **or** any of its touches is an
  audit path. `next` and `capacity` skip interactive-only tasks. This matters for the
  factory: `Invoke-Claim` retries the task `next` offers five times, so a task `next`
  offered but `move` refused would stall a lane.
- When `$env:CURL_DARK_FACTORY_LANE` is non-empty:
  - `new` without `-NoLane` whose touches include an audit path throws
    `Dark factory lanes may not file a task that touches <path>: the audit office is outside the factory's reach (ADR-NNNN). File it with -NoLane for an interactive session.`
    With `-NoLane` it is allowed: nobody in the factory can claim it (BL-999 relies on
    this to file the audit-guard task).
  - `move -To Doing` of an interactive-only task throws
    `<ID> is interactive only; a dark factory lane does not claim it.`
- When the variable is empty or absent, nothing here refuses anything: interactive
  sessions file and claim audit tasks.

Test on a scratch board (set `CLAUDE_PROJECT_DIR` to a temporary folder holding
`Tasks\Backlog`, `Tasks\Doing` and `Tasks\Done`), setting and clearing the variable per
case.

## Acceptance criteria

- [ ] With `CURL_DARK_FACTORY_LANE=2`: `new -Title x -Touches Audit/Findings` fails with a message naming `Audit/Findings`; `new -Title x -Touches .claude/agents/audit-quality.md` fails; `new -Title x -Touches Audit/Findings -NoLane` succeeds; `new -Title x -Touches Curl.Core.UnitLibrary` succeeds.
- [ ] With `CURL_DARK_FACTORY_LANE=2`: `move -Id <a lane: no task> -To Doing` fails naming the task; `move -Id <a task touching Audit/Tools> -To Doing` fails; `move -Id <an ordinary ready task> -To Doing` succeeds.
- [ ] With the variable absent, all six of the operations above succeed.
- [ ] Whatever the variable, `next` and `capacity` never print a ready task whose touches include `audit/scorecards` (lower case) and no `lane` field.
- [ ] `SKILL.md` lists the new refusals under "The script refuses" and defines audit paths.
- [ ] `task-board.ps1` contains only ASCII characters.

## Notes

## Log

- 2026-09-29: Created.
