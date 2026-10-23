---
id: BL-3154
title: Mark the merge-wait hand-over done only once the next shift has really started
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/ShiftCoordinator.ps1]
requirement: none
created: 2026-10-22
completed:
---
# BL-3154 — Mark the merge-wait hand-over done only once the next shift has really started

## Goal

A hand-over during the merge's CI wait counts as done only after `Start-Detached` really started the next shift. If the start fails, the coordinator traces the failure in red and goes on as if no hand-over happened.

## Context

Found by the shift-end review of 2026-10-22 (ADR-0710), reviewer silent-failure-hunter, in `DarkFactory/ShiftCoordinator.ps1:474` and `:488-494` (BL-3142, the merge-wait poll).

`$handOver.Done = $true` is set before `& $startNextShift ... -Beside` runs, and nothing checks what `Start-Detached` returned. Suppose the herdr tab and `Start-Process` both fail. Under `$ErrorActionPreference = 'Continue'` that failure does not stop the script, and `$next.Process` is `$null`. Then:

- The trace says "next shift started (pid )".
- Every later poll returns at once.
- `Invoke-MergeEndCiWatch -HandedOver $true` skips the final CI watch.
- The coordinator runs `exit 0`, skipping the idle poll, the `$reasons` alarm and the tab close.

So a `-Continuous` factory stops silently with exit 0, and a CI failure on the shift's last commit is never filed.

## Acceptance criteria

- [ ] `$startNextShift` reports whether a tab or a process was started. `$handOver.Done` is set only when one was.
- [ ] A failed start writes a red trace line naming the failure, and the "next shift started" line is not written.
- [ ] When the start failed, the coordinator runs the final CI watch, the idle poll and the alarm path, as if no hand-over had been attempted.
- [ ] The coordinator's self-test (or a new one in the factory's test blocks) covers a failed start during the merge wait.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b63c3000 036e81f0 83391103 6565a75d.
