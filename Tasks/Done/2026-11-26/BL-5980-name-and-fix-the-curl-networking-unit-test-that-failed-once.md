---
id: BL-5980
title: Name and fix the Curl.Networking unit test that failed once in a full fast-test run
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitTests/TcpConnectorQuicTests.LocalBinding.cs, Curl.Networking.UnitTests/StreamConnectionShutDownSendingTests.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5980 — Name and fix the Curl.Networking unit test that failed once in a full fast-test run

## Goal

The `Curl.Networking.UnitTests` test that failed once in a full fast-test run is named and no longer fails under load.

## Context

BL-5978's full fast-test run, made while other lanes were working, reported `Curl.Networking.UnitTests` `Failed: 1, Passed: 4732, Skipped: 33`; the project alone then gave `Passed: 4732, Skipped: 34`. The quiet log did not name the test. The counts suggest one that ends inconclusive when run alone; the port-taking tests in `TcpConnectorQuicTests.LocalBinding.cs` (`Assert.Inconclusive` when a port belongs to another process) are the first suspects. Widen `touches` if it is another file. BL-5978 changed nothing in Curl.Networking.

## Acceptance criteria

- [x] The Notes name the test and why it failed.
- [x] The test cannot fail that way again, `dotnet build` is clean and the fast tests pass.

## Notes

**Named:** `TcpConnectorQuicTests.UdpChannelOpener_OpenFrom_BindsTheFirstFreePortOfTheRange` and its
twin `..._WritesEachBusyPortThenTheLocalPortBound`, through their shared helper
`BindUdpSocketWhosePortIsFollowedByAFreeOne`. The name is **inferred, not observed**: BL-5978's log
did not keep it, no trx or log file survives, and the failure did not recur in this task's runs.

Why they fail under load:

- The helper held an ephemeral port P, probed P+1 and released it for `OpenFrom` to bind. Windows
  hands out ephemeral UDP ports in order (measured here: twelve port-0 binds gave 60144 to 60155),
  so a parallel test's port-0 bind takes P+1 in that gap: the test ends inconclusive.
- When P is 65535, the top of Windows' range, `new IPEndPoint(..., 65536)` throws
  `ArgumentOutOfRangeException`, which the helper's `catch (SocketException)` does not catch: the
  test fails. Read from the code, not reproduced; it is rare, so BL-5978's failure may have been
  another test.
- Counts: on Windows the project normally gives 4733 passed, 33 skipped. BL-5978's run of the
  project alone gave 4732 and 34, so one test went inconclusive there; the port-range tests are the
  load-dependent inconclusive path. Its full run (33 skipped, 1 failed) had none inconclusive.

**Fix:** the helper takes an even port in 20000 to 31999, below every platform's ephemeral range, as
the TCP twin `TcpDialerTests.HoldAPortWithTheNextFree` already does.

**Second candidate, also fixed:**
`StreamConnectionShutDownSendingTests.ShutDownSendingAsync_OverASocketWithNothingUnread_ReturnsWhileThePeerKeepsTheConnectionOpen`
would fail if the shutdown took over 2 s of wall-clock time. A blocked shutdown stays blocked while the peer is open, so the wait is now 30 s and the test
checks the same thing. Added that file to `touches`; no task in Doing named it.

Checks (2026-11-25): `dotnet build` clean (0 warnings); `dotnet format --verify-no-changes` clean
on both files; after the edits the fast tests gave 58368 passed, 0 failed, 363 skipped
(Curl.Networking.UnitTests 4733 and 33). Before the edits the project passed alone, and again on two
processors with 48 test workers (4733 and 33 both times). Test-only change: no quality run, no ADR.
If a Networking test fails again, run the project with `--logger trx` so the failure is named.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: e4f18111 77784d6a.
- 2026-11-25: Doing -> Done. UDP port-range tests take a non-ephemeral port pair and the send-shutdown test waits 30 s, so neither fails under load; the original failure's name is inferred, see Notes
