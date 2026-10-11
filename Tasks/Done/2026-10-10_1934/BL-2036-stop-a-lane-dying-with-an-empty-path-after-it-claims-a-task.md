---
id: BL-2036
title: Stop a lane dying with an empty Path after it claims a task its own integration just renumbered
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-2036 — Stop a lane dying with an empty Path after it claims a task its own integration just renumbered

## Goal

A lane that claims a task its own integration just renumbered runs that task instead of dying with a script error.

## Context

On 2026-10-10, lane 1 of the shift that started at 16:13 (Z:\repos\Curl.logs\DarkFactory-20261010-161355-L1.log) went through these steps:
- 16:26:11: integrated BL-1976 and renumbered the task it had filed from BL-2033 to BL-2035 ("renum").
- 16:26:16: requeued BL-1976, which waits on BL-2035.
- 16:26:28: claimed BL-2035.
- 16:27:01: died with "ERROR Cannot bind argument to parameter 'Path' because it is an empty string" and "shift end (script error) ... stalled=1".

The shift then ended with the alarm, leaving BL-2035 in Doing. Nothing ran until an interactive restart at 18:17, which lost 1.5 hours. The empty Path is most likely a task file looked up under the pre-renumber name, or a lookup made before the renumbered file reached the lane's worktree. Start at the claim's run path in RunDarkFactory.ps1 (`Invoke-Claim`, then the code that finds the claimed task's file, model and pipeline).

## Acceptance criteria

- [x] The cause of the empty Path is named in the commit message, and the claimed task's file is found by its ID after any renumbering.
- [x] A lane script error on one task no longer ends the shift with the task left in Doing. The coordinator restarts the lane, which adopts the task, as it does for a lane process that dies.
- [x] A self-test or script test reproduces renumber-then-claim and shows the task runs.

## Notes

- Cause: not the renumber. BL-2035's run made an Edit whose input named its file as `path`, not `file_path`; `Get-ToolLabel` ran `Split-Path ""` on the empty `file_path` inside `Write-Event`, which threw out of the lane loop (the error came 33 s into the run, mid-stream). Fixed with `Get-ToolFileName` (file_path, path, notebook_path, else empty), a try/catch around event tracing, and a lane that hits any script error with its task in Doing now resumes it (twice at most) instead of ending the shift.
- Test: `RunDarkFactory.ps1 -TestToolLabels`. The renumber-then-claim path is covered by `-TestClaimDuplicates` (BL-2014); the failing step was the run's event trace, not the claim.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. Empty Path was Split-Path on an Edit input naming path; event tracing no longer kills a lane, and a script error resumes the task in Doing
