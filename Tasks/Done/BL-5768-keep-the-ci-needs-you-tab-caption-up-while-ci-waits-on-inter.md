---
id: BL-5768
title: Keep the CI needs you tab caption up while CI waits on interactive-only tasks
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/CiWatch.ps1]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5768 — Keep the CI needs you tab caption up while CI waits on interactive-only tasks

## Goal

While a red CI run waits only on interactive-only tasks, the coordinator's tab reads "CI needs you, read" on every heartbeat, even after another step of the shift has captioned the tab with a routine text.

## Context

- `DarkFactory/CiWatch.ps1:910-925` (`Update-CiNeedsYou`, added this shift for AF-0961/AF-0973) applies the caption only on the false-to-true change of `$script:CiWatch.NeedsYou`, and returns early on later calls while the flag stays true.
- Other steps caption the same tab after the CI watch has run: `ShiftCoordinator.ps1:874` (`'idle, polling'` at shift end, while the watch keeps polling), `ShiftCoordinator.ps1:395` (`''` after the fresh-session wait), `OutOfTokens.ps1:170` and `:173` (after the token wait). After any of them the caption is gone, `NeedsYou` is still true, and it never comes back. That is the unnoticed 227-minute red spell AF-0973 was meant to stop.
- The self-test in `CiWatch.ps1` (`'needing you is traced and captioned once, ...'`) does not recaption between its two "needs you" calls, so it misses this.
- Found by the shift-end review of 2026-11-22 (pr-review-toolkit silent-failure-hunter and code-reviewer, review-20261122-144529).

## Acceptance criteria

- [x] While `Get-CiNeedsYouLine` gives a line, `Update-CiNeedsYou` captions the tab "CI needs you, read" again whenever `$script:OwnTabText` has been set to a routine caption (`''`, `'idle, polling'` or a task title) since; it leaves a caption that starts with `ALARM` or `WAITING` alone.
- [x] The trace line is still written once per change of the line, not once per heartbeat.
- [x] A new `-TestCiWatch` check recaptions the tab to `'idle, polling'` between two "needs you" calls and expects "CI needs you, read" back; the existing `Update-CiNeedsYou` checks still pass.
- [x] `RunDarkFactory.ps1 -TestCiWatch` passes.

## Notes

Update-CiNeedsYou recaptions when OwnTabText is a routine caption; TabBefore becomes that caption. Trace line unchanged (once per line change). New check added; -TestCiWatch passes.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 4f669472 af88ed69 51a52ae7 49318181.
- 2026-11-22: Doing -> Done. Update-CiNeedsYou recaptions after a routine caption
