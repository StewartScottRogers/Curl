---
id: BL-3093
title: Wait for a dirty checkout to clean at a -Continuous handover instead of alarming at once
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3091, BL-3092]
touches: [DarkFactory/Park.ps1, DarkFactory/ShiftCoordinator.ps1, DarkFactory/ShiftRunner.ps1]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3093 — Wait for a dirty checkout to clean at a -Continuous handover instead of alarming at once

## Goal

A shift that starts at a `-Continuous` hand-over and finds the main checkout dirty logs the dirty paths, retries every 2 minutes for up to 30 minutes, starts as soon as the tree is clean, and alarms only if it is still dirty after that.

## Context

Evidence: 2026-10-22 06:15:11 (`DarkFactory-20261022-061510.log`): "refuse working tree not clean; commit or stash first", then ALARM stages 1-4 and nothing more. An interactive session's commits had left the checkout briefly dirty and it was clean again minutes later; the factory stayed stopped.

Where: the check is inline in two places: `DarkFactory/ShiftCoordinator.ps1` line 10 and `DarkFactory/ShiftRunner.ps1` line 16 (`if (Get-Dirty) { Stop-ShiftStart 'working tree not clean; commit or stash first'; exit 1 }`); `Get-Dirty` is in `DarkFactory/AffectedTests.ps1` (git status --porcelain), `Stop-ShiftStart` in `DarkFactory/Park.ps1` line 70. Add `Wait-ForCleanTree` to `DarkFactory/Park.ps1` (beside `Stop-ShiftStart`) taking injected `Get-Dirty`, sleep and clock scriptblocks: when the shift was started by a hand-over (`$ShiftBranch` is set, i.e. `-Continuous` forwarded it) it traces each dirty path (first 10, then "and N more") once, retries every 2 minutes up to 30 minutes, returns clean as soon as `Get-Dirty` is empty, and returns the dirty list after the window; both call sites use it and call `Stop-ShiftStart` (alarm) only on a still-dirty result. A shift not started by a hand-over keeps today's immediate refusal. It must never stash, reset, checkout or otherwise touch the user's changes; read-only `git status` only.

Depends on BL-3092 (shares `ShiftCoordinator.ps1`) and BL-3091 (shares `ShiftRunner.ps1`), so it lands after both.

## Acceptance criteria

- [x] `Wait-ForCleanTree` exists in `DarkFactory/Park.ps1` and both `ShiftCoordinator.ps1` and `ShiftRunner.ps1` call it instead of the inline `Get-Dirty` refusal.
- [x] `RunDarkFactory.ps1 -TestPark` (extended) passes with injected-clock cases: dirty then clean after 3 polls returns clean having slept 3 times 2 minutes; always dirty returns the dirty list after exactly 30 minutes of injected time (15 polls) and the caller alarms; clean at once does not sleep; a non-hand-over start refuses at once without sleeping.
- [x] The same self-test shows the dirty paths are traced and the trace is not repeated on every poll.
- [x] A grep of the new code finds no `git stash`, `git reset`, `git checkout`, `git clean` or `Remove-Item` call.
- [x] All `RunDarkFactory.ps1 -Test*` switches that passed before still pass.

## Notes

- Delivered directly (a PowerShell change to three DarkFactory scripts; no protocol-architect plan needed). `Wait-ForCleanTree` in `DarkFactory/Park.ps1` takes injected `GetDirty`, `Sleep`, `Now` and `Trace` scriptblocks; hand-over is `[bool]$ShiftBranch`. It polls at t=0,2,...,30 minutes and gives up at the poll that sees 30 minutes elapsed (15 sleeps), as the criteria ask. Both call sites keep the same refusal text, so an alarm reads as before; the trace adds the dirty paths once, a "wait" line, and a "still not clean after 30 minutes" line.
- Checked: `-TestPark` 14 cases pass (5 new); all 23 `RunDarkFactory.ps1 -Test*` switches exit 0; a grep of the added lines finds no git stash/reset/checkout/clean or Remove-Item; dotnet build clean and the fast tests green.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: eb6f06bd 81817d88 0fbe131c 8f7f27e9 7193fd91.
- 2026-10-22: Doing -> Done. A -Continuous hand-over that finds the checkout dirty traces the paths once, retries every 2 minutes for 30 minutes, and alarms only if still dirty
