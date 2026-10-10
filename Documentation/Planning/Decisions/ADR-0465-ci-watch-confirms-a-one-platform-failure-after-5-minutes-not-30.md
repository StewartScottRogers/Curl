# ADR-0465: The CI watch confirms a one-platform failure after 5 minutes, not 30

- Status: Accepted
- Date: 2026-10-10
- Task: BL-1969 (audit finding AF-0150)
- Decided by Claude under Stewart's delegation.

## Context

`RunDarkFactory.ps1`'s CI watch files a failure seen on one platform in only the newest run
once another run confirms it, or once `$CiConfirmMinutes` (30) have passed. Lanes test only on
Windows, so a Linux- or macOS-only failure is invisible to them until the watch files it. In
the AF-0150 spell, test4001 failed on macOS and the coordinator logged "waiting for the next
run" for 17 minutes before it filed BL-1949; CI stayed red 33.92 minutes in all.

## Decision

`$CiConfirmMinutes` is 5. The watch still waits for a second run or the window before it
calls a one-platform failure a regression, so a one-off flake is still told from a break, but
the task is filed within one or two heartbeats (3 minutes) instead of a half hour. Two-platform,
build and repeated failures were already filed at once and are unchanged. The wait is the
window the watch files at, not a measured flake rate: a flake filed early becomes a
regression-titled task that a lane closes by finding it passes, which costs less than a red
`master` merge gate.

## Consequences

The Get-CiVerdicts help, the `-TestCiWatch` self-test and its names say 5 minutes.
