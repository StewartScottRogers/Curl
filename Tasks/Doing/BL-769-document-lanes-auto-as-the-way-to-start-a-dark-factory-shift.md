---
id: BL-769
title: Document -Lanes Auto as the way to start a dark factory shift
priority: High
assignee: Claude
pipeline: docs
depends-on: [BL-768]
touches: [RunDarkFactory.ps1, CLAUDE.md]
requirement: none
created: 2026-09-28
completed:
---
# BL-769 — Document -Lanes Auto as the way to start a dark factory shift

## Goal

The script header, `CLAUDE.md` and Stewart's memory all say to start shifts with `RunDarkFactory.cmd -NewTab -Lanes Auto -Continuous`. They explain in a few lines how Auto sizes a shift, and each statement is true of the code BL-768 left.

## Context

- Stewart approved `-Lanes Auto` on 2026-09-28. He moves between Claude plans by affordability, so a fixed lane count is wrong on every plan but one. The ADR from BL-763 is the design, and BL-764 to BL-768 built it. Read the ADR and the script as it now is before writing. Describe the code, not the plan: if they differ, the code wins, and the difference is noted in the task's Log.
- **`RunDarkFactory.ps1` header** (the `<# ... #>` block at the top):
  - Add an `AUTO LANES (-Lanes Auto)` section after `PARALLEL LANES`, of about 10 to 15 lines. It covers:
    - the 15-minute step;
    - the per-lane burn rate from the plan-relative utilization, so there is no plan setting;
    - the 5-hour and weekly pace targets, with `-NoWeeklyPace`;
    - the ceilings: `task-board.ps1 capacity`, the machine cap from `<repo>.lanes\machine-lanes.json` and `-ProbeMachine`, and `-MaxLanes`;
    - one lane per step, and a retiring lane finishing its task first;
    - the cold start from `<repo>.lanes\auto-lanes.json`;
    - the `lanes a -> b (reason)` trace line;
    - `-AutoLanesReport`.
  - Make `powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -Lanes Auto -Continuous` the first lane example in `.EXAMPLE`.
  - Only comments change. No code line changes.
- **`CLAUDE.md`, "Dark factory"** (near line 87):
  - Extend the `-Lanes N` sentence: `-Lanes Auto` sizes the shift itself, adding or retiring one lane at a time to the measured burn rate, the board's parallel capacity and the machine's cap.
  - Change the start example from `RunDarkFactory.cmd -NewTab -Lanes 3 -Continuous` to `RunDarkFactory.cmd -NewTab -Lanes Auto -Continuous`.
  - Leave the rest of the section as it is, except for anything now untrue.
- **Stewart's memory.** These files are outside the repository and are not committed. Both are in `C:\Users\Stewart Rogers\.claude\projects\Z--repos-Curl\memory\`:
  - `run-dark-factory-in-herdr.md`:
    - its `description` and body give the start command `RunDarkFactory.cmd -NewTab -Lanes Auto -Continuous`;
    - "Stewart set 3 lanes on 2026-09-27" becomes a line saying Auto replaced the fixed count on 2026-09-28, because he changes plans;
    - the stale path `logs\lanes-<stamp>\lane-<n>.task` becomes `<repo>.logs\lanes-<stamp>\lane-<n>.task`, where the script now keeps it.
  - Its `MEMORY.md` index line says `-Lanes Auto` instead of `-Lanes 3`.
  - `dark-factory-sizes-lanes-to-plan.md`: its "How to apply" drops "Until the auto-lanes tasks land..." and says shifts start with `-Lanes Auto`.
- A dark factory shift may be running. Nothing here changes behaviour, and the script's code lines stay as they are.

## Acceptance criteria

- [ ] `RunDarkFactory.ps1`'s header has an `AUTO LANES (-Lanes Auto)` section naming each item listed in Context. Its first lane `.EXAMPLE` line is the `-Lanes Auto -Continuous` one. `git diff RunDarkFactory.ps1` changes only lines inside the header comment block.
- [ ] `CLAUDE.md` "Dark factory" contains `RunDarkFactory.cmd -NewTab -Lanes Auto -Continuous` and a sentence describing Auto as above. `Select-String -Path CLAUDE.md -Pattern '-Lanes 3'` finds nothing.
- [ ] `run-dark-factory-in-herdr.md`, its `MEMORY.md` line and `dark-factory-sizes-lanes-to-plan.md` read as described in Context. No file under that memory folder still recommends `-Lanes 3`.
- [ ] `[System.Management.Automation.Language.Parser]::ParseFile` reports no errors for `RunDarkFactory.ps1`, and `-TestAutoLanes` still exits 0.
- [ ] `git diff --stat` shows only `RunDarkFactory.ps1` and `CLAUDE.md` changed outside `Tasks/`.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
