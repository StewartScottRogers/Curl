---
id: BL-2636
title: Make upstream case 753 run: the runner gives a libtest case the Debug feature it needs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2636 — Make upstream case 753 run: the runner gives a libtest case the Debug feature it needs

## Goal

Upstream case 753 runs through the runner and, when it passes, holds the ratchet.

## Context

Found by BL-2536, which ported lib753 (`Curl.Conformance.LibTest.UnitLibrary/Lib753.cs`) and unit tested it over a fake engine. test753 is skipped: `test753 is skipped: Curl lacks the feature Debug`. The case needs `<features>Debug ftp</features>` and sets `CURL_FTP_PWD_STOP=1`, a debug-build switch that stops libcurl's FTP state machine after `PWD`; the program cleans EASY1 up without removing it from the multi handle and checks, through the `<protocol>` lines, that EASY2 does not reuse its connection (USER/PASS twice, then PWD, EPSV, TYPE I, SIZE 753, QUIT). In the first pass decide (ADR if it is a behaviour choice) whether Curl's FTP engine honours `CURL_FTP_PWD_STOP` and whether the runner may then grant the Debug feature for this case, find the project at fault and add it to `touches` before any edit.

## Acceptance criteria

- [x] test753 is run (not skipped) by `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` with `CURL_UPSTREAM_CASE=753`; if it passes on Windows it is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, else it is filed with the board script with its differing lines.
- [x] The fix adds a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Touches widened at the start (no other task in Doing on origin/work/dark-factory): the fix is in
  `Curl.Protocol.Ftp.UnitLibrary` (stop before `PWD`), `Curl.Engine.UnitLibrary` (the first-perform claim, and
  `curl_easy_cleanup` leaving the multi handle) and `Curl.Conformance.UnitTests` (Debug granted, engine wired), with
  their `.UnitTests`.
- Upstream (`lib/ftp.c`, `ftp_state_pwd`): `#ifdef DEBUGBUILD if(!data->id && getenv("CURL_FTP_PWD_STOP")) return CURLE_OK;`
  so the first transfer sends no `PWD` and waits. Decided (ADR-0687): only the harness turns the stand-in on, through
  `ManagedCurlEngine`'s internal constructor; release-like Curl ignores the variable as release curl does.
- First run after the stand-in hung (curl did not finish within 20 seconds): the engine kept a disposed handle in
  its multi handle. `ManagedEasyTransfer.DisposeAsync` now removes it first, as libcurl does. test753 then passed.
- Unit tests: `FtpProtocolHandlerTimeLimitTests.ExecuteAsync_StopsBeforeEntryPath_SendsNoPwdAndWaitsUntilTheConnectTimeout`,
  `FirstTransferFtpPwdStopTests` (3), `ManagedCurlEngineTests` (4 new). Measure-CodeQuality: 0 failing members in
  `Curl.Engine.UnitLibrary` and `Curl.Protocol.Ftp.UnitLibrary` (the stall uses `ConfigureAwaitOptions.SuppressThrowing`
  so no line sits after an await that never returns). Output is FTP protocol lines only, no paths or OS text, so the
  case is platform-neutral.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 22b35c88 c59fcab3 3d836d5e.
- 2026-10-18: Doing -> Done. Upstream case 753 runs and passes and holds the ratchet: the harness stands in for CURL_FTP_PWD_STOP and curl_easy_cleanup leaves the multi handle
