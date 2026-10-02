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
completed: 2026-10-01
---
# BL-1141 — Fast-forward a shift's checkout that is only behind origin instead of refusing to start

## Goal

A coordinator whose checkout is only behind `origin/<branch>` fast-forwards it and starts the shift, so a `-Continuous` shift change never stops on an alarm.

## Context

- On 2026-10-01 the 11:41 shift ended at 19:41 and `-Continuous` started the next at 19:51. It refused with `work/dark-factory differs from origin/work/dark-factory; push or pull first` and sounded its alarm: the checkout was 1 commit behind, because a lane pushed after the coordinator last synced. No lane ran from 19:41 until an interactive session pulled and restarted the shift at 19:53 (`Curl.logs/DarkFactory-20261001-195130.log`).
- The check is in `RunDarkFactory.ps1`'s coordinator start, after `git -C $Root fetch -q origin $branch`: it compares `HEAD` with `origin/$branch` and calls `Stop-ShiftStart` on any difference.
- Behind only (HEAD is an ancestor of `origin/$branch`) with a clean tree is safe to fast-forward: `git merge --ff-only origin/$branch`. Ahead or diverged still needs a person (a push or a decision), so it keeps refusing. The clean-tree check (`Get-Dirty`) already runs before this one.

## Acceptance criteria

- [x] When `HEAD` is an ancestor of `origin/$branch` and differs from it, the coordinator runs `git merge --ff-only origin/$branch`, writes a trace line saying how many commits it fast-forwarded, and starts the shift.
- [x] When `HEAD` is ahead of or diverged from `origin/$branch`, it still calls `Stop-ShiftStart` with a message saying which (ahead or diverged), and the alarm still sounds.
- [x] A failed `merge --ff-only` calls `Stop-ShiftStart` rather than starting on a stale checkout.
- [x] Checked by hand in a scratch clone: one commit behind starts; one commit ahead refuses with "ahead"; diverged refuses with "diverged". Notes records the three results.
- [x] The script header's description of the start checks says behind-only is fast-forwarded.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- The check moved into `Sync-CheckoutWithOrigin` (beside `Get-Dirty`), which returns the refusal text or `$null`; the coordinator start passes any refusal to `Stop-ShiftStart`, so the alarm still sounds. Behind is detected with `git merge-base --is-ancestor HEAD origin/<branch>`, ahead with the reverse test, and anything else is diverged. The lone-runner start never compared with origin, so it is unchanged.
- Hand check in a scratch clone (bare origin plus two clones, the function extracted from the script's AST): one commit behind -> trace `fast-forwarded work 1 commit(s) to origin/work`, starts; one ahead -> refuses `work is ahead of origin/work; push or reconcile first`; diverged -> refuses `work has diverged from origin/work; push or reconcile first`.
- A failed `merge --ff-only` returns `... git merge --ff-only failed; pull first`, which refuses the start.
- `dotnet build` clean; fast tests green (0 failed).

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. a coordinator whose checkout is only behind origin fast-forwards it and starts; ahead or diverged still refuses with the alarm
