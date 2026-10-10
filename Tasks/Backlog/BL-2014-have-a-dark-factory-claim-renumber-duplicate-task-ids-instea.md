---
id: BL-2014
title: Have a dark factory claim renumber duplicate task IDs instead of reporting lost races
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-10
completed:
---
# BL-2014 — Have a dark factory claim renumber duplicate task IDs instead of reporting lost races

## Goal

When two live tasks share an ID, a lane's claim renumbers them and goes on claiming, so a shift no longer idles for hours.

## Context

On 2026-10-10 duplicate IDs stalled every lane twice: 08:00-09:23 (BL-1959..BL-1971, from the audit and the gap run) and 09:47-13:20 (BL-2010, from a lane and an interactive session). `task-board.ps1 move` refuses an ID that names two live tasks, so `Invoke-Claim` in RunDarkFactory.ps1 saw the task never reach Doing, retried 5 times and logged only "claim kept losing races", which named the wrong cause. The fix is in RunDarkFactory.ps1 only; task-board.ps1 is a guard file and stays as it is.

## Acceptance criteria

- [ ] When `move -To Doing` fails because the ID names more than one live task, `Invoke-Claim` runs `task-board.ps1 dedupe -Since` an earlier commit of the branch, commits and pushes the renumbering, traces "renumbered duplicate task IDs <old> -> <new>", and claims again.
- [ ] A claim that fails for any other reason logs that reason instead of "claim kept losing races"; that text is used only when a push of the claim was refused.
- [ ] A Pester-free PowerShell self-test, or a test in the factory's existing script tests, shows a board with a duplicate ID ends with the task claimed and no duplicate left.

## Notes

## Log

- 2026-10-10: Created.
