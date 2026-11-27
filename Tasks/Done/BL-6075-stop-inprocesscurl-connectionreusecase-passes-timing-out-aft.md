---
id: BL-6075
title: Stop InProcessCurl_ConnectionReuseCase_Passes timing out after 20 seconds in a loaded test run
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitTests/UpstreamConformanceTests.ConnectionReuseCases.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-27
---
# BL-6075 — Stop InProcessCurl_ConnectionReuseCase_Passes timing out after 20 seconds in a loaded test run

## Goal

`InProcessCurl_ConnectionReuseCase_Passes` passes in a whole-solution fast test run on a loaded machine.

## Context

On 2026-11-26, in BL-6073's whole-solution `dotnet test` on lane 12 with other lanes building,
cases (471,False), (435,False), (1074,False), (338,False) and (1074,True) failed after 22 s with
`curl did not finish within 20 seconds`. All 20 cases passed in 1 s when run alone. Not seen in CI.
Start at `Curl.Conformance.UnitTests/UpstreamConformanceTests.ConnectionReuseCases.cs` and the time
limit `UpstreamCaseRunner` is given.

## Acceptance criteria

- [x] The cause of the 20 second stall (a starved thread pool, a blocking wait, or only too short a limit) is recorded under Notes.
- [x] The reuse cases no longer fail on a time limit a loaded run exceeds, and they pass.

## Notes

- Cause: a stall of the whole test process on a loaded machine, most likely a starved thread pool.
  This is inferred, not reproduced. Not only too short a limit for the cases' own work: in a
  whole-solution fast run on 2026-11-27 each of the 20 rows took 0.2 s to 0.5 s against the 20 second
  limit, and in the failing run five rows (both dial paths) ran out together at 22 s. No blocking wait
  was found on these cases' path: the blocking waits a search of the conformance projects finds are
  libtest programs (`Lib1517`, `Lib3207`, `LibTestClock`) and test code, none run by these rows,
  though the ratchet runs those programs in the same process. Curl starts on a thread of its own
  (BL-1062), but its continuations and the time limit's timer need pool threads, which the parallel
  conformance rows share. BL-1062 found that capping the pool starves the test host itself, so no
  reproduction was tried.
- Fix: all ten cases are on `PassingUpstreamCases.txt`, where the ratchet already gives a failed case a
  second attempt for such a stall (BL-1859, BL-2093, BL-6069), but this test did not. It now shares
  `RunAgainIfFailedAsync`, each attempt with its own log folder, and writes the first failure to the
  test's diagnostics, so a case that really broke still fails. The 20 second limit is unchanged. No
  ADR: it extends the existing rule, as BL-6069 did. `RunAgainIfFailedAsync` has its own tests.
- Verified: `dotnet build` clean (0 warnings), the whole fast suite green, format check clean. Test-only
  change, so no quality measurement.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 095f8b5c 32f92ed9.
- 2026-11-27: Doing -> Done. InProcessCurl_ConnectionReuseCase_Passes gives a failed case a second attempt, as the ratchet does, so a stalled test process no longer fails it.
