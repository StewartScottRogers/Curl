---
id: BL-1141
title: Fast-forward a shift's checkout that is only behind origin instead of refusing to start
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-01
completed:
---
# BL-1141 — Fast-forward a shift's checkout that is only behind origin instead of refusing to start

## Goal

A coordinator whose checkout is only behind `origin/<branch>` fast-forwards it and starts the shift, so a `-Continuous` shift change never stops on an alarm.

## Context

- On 2026-10-01 the 11:41 shift ended at 19:41 and `-Continuous` started the next at 19:51. It refused with `work/dark-factory differs from origin/work/dark-factory; push or pull first` and sounded its alarm: the checkout was 1 commit behind, because a lane pushed after the coordinator last synced. No lane ran from 19:41 until an interactive session pulled and restarted the shift at 19:53 (`Curl.logs/DarkFactory-20261001-195130.log`).
- The check is in `RunDarkFactory.ps1`'s coordinator start, after `git -C $Root fetch -q origin $branch`: it compares `HEAD` with `origin/$branch` and calls `Stop-ShiftStart` on any difference.
- Behind only (HEAD is an ancestor of `origin/$branch`) with a clean tree is safe to fast-forward: `git merge --ff-only origin/$branch`. Ahead or diverged still needs a person (a push or a decision), so it keeps refusing. The clean-tree check (`Get-Dirty`) already runs before this one.

## Acceptance criteria

- [ ] When `HEAD` is an ancestor of `origin/$branch` and differs from it, the coordinator runs `git merge --ff-only origin/$branch`, writes a trace line saying how many commits it fast-forwarded, and starts the shift.
- [ ] When `HEAD` is ahead of or diverged from `origin/$branch`, it still calls `Stop-ShiftStart` with a message saying which (ahead or diverged), and the alarm still sounds.
- [ ] A failed `merge --ff-only` calls `Stop-ShiftStart` rather than starting on a stale checkout.
- [ ] Checked by hand in a scratch clone: one commit behind starts; one commit ahead refuses with "ahead"; diverged refuses with "diverged". Notes records the three results.
- [ ] The script header's description of the start checks says behind-only is fast-forwarded.
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
