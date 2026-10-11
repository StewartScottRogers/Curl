---
id: BL-2028
title: Find and fix the flaky Curl.Networking.UnitTests test that failed once in a full fast run and passed alone
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-2028 — Find and fix the flaky Curl.Networking.UnitTests test that failed once in a full fast run and passed alone

## Goal

Curl.Networking.UnitTests passes in every full `dotnet test --filter "TestCategory!=Integration"` run, not only when run alone.

## Context

- Seen on 2026-10-10 by BL-1993 (lane 5): the full fast run reported `Failed: 1, Passed: 3151, Skipped: 29` for Curl.Networking.UnitTests; the project run alone right after passed. BL-1993 changed only the SMTP library, so the failure is not its. The failing test's name was not captured (the run's output was filtered to the summary lines).
- Start by running the full fast suite with `--logger trx` and reading the failing test from the TRX, or by looking for tests in the project that depend on timing, ports or shared state under load.

## Acceptance criteria

- [x] The flaky test is named in Notes with its cause, and fixed so it no longer depends on timing or load.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Found by running 20 copies of the project's fast tests at once (one in 20 failed; 8 at once and 1 alone all passed).
- Flaky test: `TcpConnectionListenerTests.DisposeAsync_ReleasesThePortSoItCanBeBoundAgain` (`Assert.AreEqual(CurlExitCode.Ok, second.ExitCode)` got `FtpPortFailed`).
- Cause: it bound an ephemeral port, released it, then bound that same port again; under load another test or process took the port in between.
- Fix: the test retries with a fresh ephemeral port (up to 20 attempts) when the rebind fails, so only a release that never works fails it.
- Also seen under load, not failures: `UdpChannelOpener_OpenFrom_BindsTheFirstFreePortOfTheRange` and `..._WritesEachBusyPortThenTheLocalPortBound` go Inconclusive (skipped) by design when the next port is taken.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. TcpConnectionListener port-release test retries with a fresh port, so it no longer flakes under load
