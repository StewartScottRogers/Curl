---
id: BL-1734
title: File lane-eligible Curl tasks for open gap findings with Gap/Tools/New-TasksFromGaps.ps1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1720]
touches: [Gap/Tools/New-TasksFromGaps.ps1, Gap/Tools/Fixtures/tasks, Gap/Triage.md, Gap/README.md]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1734 — File lane-eligible Curl tasks for open gap findings with Gap/Tools/New-TasksFromGaps.ps1

## Goal

`Gap/Tools/New-TasksFromGaps.ps1` files one lane-eligible Curl task on the factory's board
for each open target-scope gap finding that has no open task. It also files a `Re-close`
task when a finding's tasks are all Done but the latest run still measures it. It writes
each task's ID back into the finding. `Gap/Triage.md` explains the flow.

## Context

This is ADR-0433 decision 4: Claude accepts the suggestions and files the tasks itself, with
no approval from Stewart. Stewart may set any finding to `rejected`, and a rejected finding
gets no new task. The finding format is in `Gap/Instructions/Gap-Format.md` and
`Gap/Findings/README.md` (BL-1720).

Tasks are created only with the board script, `.claude/skills/task-board/task-board.ps1 new`.
Read `.claude/skills/task-board/SKILL.md`. Then fill the body by rewriting the template's
`Goal`, `Context` and `Acceptance criteria`, so none of the template's HTML comments
survives.

**Parameters.** `-FindingsDirectory` (default `Gap/Findings`), `-BoardRoot` (the checkout
whose `Tasks/` receives the tasks, default the repository root; the orchestrator BL-1741
passes a `work/dark-factory` worktree), `-WhatIf`, `-SelfTest`. The script refuses to run
when `CURL_DARK_FACTORY_LANE` is set. A lane must never file from the office.

**Mapping.**

| Finding | Task |
| --- | --- |
| `status: open`, `scope: target`, no open task in `tasks` | Title `Close GF-####: <title>` |
| Every task Done (in `Tasks/Done` or its archive) and the latest `Measurements` line, dated after the last completion, still shows gaps | Title `Re-close GF-####: <title>` |
| `scope: newest`, `rejected` or `closed` | No task |
| Severity Critical or High | Priority `High` |
| Severity Medium | Priority `Normal` |
| Severity Low | Priority `Low` |
| Area `protocols` | Pipeline `protocol` |
| Any other area | Pipeline `feature` |
| The finding's `touches` | `-Touches`, with any `Gap/`, `Audit/` or `.claude/agents/` path dropped. When none is left, file with no touches, so the task runs alone, and say so in the task's Context |

**The task body.** Lanes cannot read `Gap/` once BL-1746 guards it, so the task must stand
alone:

- Goal: what Curl must do so the finding's items measure `match`.
- Context: the finding's ID, area, severity, `introduced-in`, its item keys, its Evidence
  section and its Suggestion section copied in full, the targeted curl version, and the
  upstream document the yardstick came from. It also says the gap closes only when a later
  gap analysis re-measures it (ADR-0433 decision 5).
- Acceptance criteria: one box per item key, stating the observable result the item needs
  (taken from the item's `expected`), plus the standard gates (`dotnet build -warnaserror`
  clean, fast tests green, and `--ai-help` kept right when an option changes, CLAUDE.md).

After filing, set the finding's `task` to the new ID, append it to `tasks`, and add a `Log`
line. A `Close` or `Re-close` task filed by hand (its title names the finding) counts as an
open task, so the script files nothing more for that finding.

`Gap/Triage.md` states, in order: findings arrive `open`; this script files their tasks;
Stewart may reject any finding; a finding closes only on re-measurement; when the fix does
not hold, a `Re-close` task is filed; `scope: newest` findings wait for a retarget ADR.

`task-board.ps1` takes its board root from `$env:CLAUDE_PROJECT_DIR` when that is set (line
112), and reads its template from beside itself. Point `CLAUDE_PROJECT_DIR` at `-BoardRoot`
for each call, and restore it afterwards.

**Self-test.** A temporary copy of a fixture board (`Gap/Tools/Fixtures/tasks/board/`, with
empty `Tasks/Backlog`, `Doing` and `Done` folders) and fixture findings. The self-test
calls the real `task-board.ps1` with `CLAUDE_PROJECT_DIR` set to the copy.

## Acceptance criteria

- [x] `Gap/Tools/New-TasksFromGaps.ps1 -SelfTest` prints `PASS` lines and no `FAIL` under Windows PowerShell 5.1 and PowerShell 7. It checks each mapping row; a filed task contains the finding's Evidence and Suggestion text and none of the template's HTML comments; the finding gains `task`, `tasks` and a `Log` line; a hand-filed `Close GF-####` task stops a second filing; dropped `Gap/` touches; the refusal when `CURL_DARK_FACTORY_LANE` is set.
- [x] Every task the self-test files passes `task-board.ps1 status` with no warning, and none says `lane: no`.
- [x] `Gap/Triage.md` states the six steps above.
- [x] `-WhatIf` changes nothing. The header help documents every parameter and the mapping. The script is ASCII only.

## Notes

- Added `Gap/README.md` to `touches`: its `Triage.md` row said "planned, BL-1734", false once this lands. No task in Doing on `origin/work/dark-factory` names it.
- Choices taken (sensible defaults, Claude under Stewart's delegation):
  - "Dated after the last completion" compares the Measurements line's date with the latest `completed` date strictly: a run on the completion day may predate the fix, and the next run catches it.
  - A finding's tasks are its `tasks` list plus any board task titled `Close GF-####` / `Re-close GF-####` for it; open means Backlog, Doing, Blocked or Deferred. A listed ID that is on no board files nothing (the board passed in may lag).
  - An item's acceptance box gives the item's `expected` answer when the optional `-MeasurementsDirectory` (a run's `measurements/` folder) holds it; otherwise it points at the copied Evidence. Findings themselves carry no `expected`.
  - The yardstick is the `sources` of `Gap/Upstream/<target>/<area>.json`, else the release tarball named generically (behaviour has no inventory yet).
  - `task-board.ps1 new` runs as a child of the same PowerShell edition (`$PSHOME`, since the process may be a dotnet host) with `CLAUDE_PROJECT_DIR` set to `-BoardRoot` and restored.
  - `-SelfTest` itself runs inside a lane; its inner runs clear `CURL_DARK_FACTORY_LANE`, except the one checking the refusal.
- Verified: `-SelfTest` 21 PASS, 0 FAIL under Windows PowerShell 5.1 and PowerShell 7; ASCII only; `dotnet build -warnaserror` clean; fast tests green.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Gap/Tools/New-TasksFromGaps.ps1 files Close and Re-close tasks for open target gap findings and writes their IDs back; Gap/Triage.md explains the flow
