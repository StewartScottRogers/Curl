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
completed:
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

- [ ] The cause of the empty Path is named in the commit message, and the claimed task's file is found by its ID after any renumbering.
- [ ] A lane script error on one task no longer ends the shift with the task left in Doing. The coordinator restarts the lane, which adopts the task, as it does for a lane process that dies.
- [ ] A self-test or script test reproduces renumber-then-claim and shows the task runs.

## Notes

## Log

- 2026-10-10: Created.
