---
id: BL-898
title: Rerun the fast tests once before parking a lane's work, and log the failing test names
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-09-29
completed:
---
# BL-898 — Rerun the fast tests once before parking a lane's work, and log the failing test names

## Goal

A lane parks its work only when the fast tests fail twice in a row after a rebase. Every
failing run's test names reach the lane's trace, and the park reason names them.

## Context

Investigated 2026-09-29. All 14 parks overnight (BL-564, 592, 610, 738, 751, 789, 792,
816, 825, 828, 852, 857, 859, 876) had the reason "fast tests failed after rebasing onto
the other lanes' work", and none was a conflict. Rerunning BL-738's parked commit failed
one QUIC test, `QuicConnectionTests.Loop_ServerGoesSilent_SendsKeepAlivesThenFailsWithTheIdleTimeout`,
which a cryptography change cannot affect. The same test passed 15 of 15 runs on its own,
and the whole shared branch passed three full runs out of three, so the tests are flaky
under load rather than broken. `Test-Green` pipes the test output to `Out-Null`, so the
other 13 failures left no names behind. Each park throws a finished paid run away.

## Acceptance criteria

- [ ] `Test-Green` keeps the `dotnet test` output and returns the failing test names
      (lines `  Failed <name>`) and failing assemblies alongside its reason.
- [ ] After a red fast-test run, the lane traces `flaky?` with the names and runs the fast
      tests once more. Green on the rerun integrates as normal, with a yellow trace
      naming the tests that failed only once. Red again parks, with the names from both
      runs in the park reason.
- [ ] A build failure still parks at once, since rebuilding does not change it.
- [ ] A pure `Get-FailedTestNames` extracts names from test output, and `-TestFlakyTests`
      proves it on recorded output (names, the summary line of an aborted host, no
      failures).
- [ ] The script parses, and its self-checks pass.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
