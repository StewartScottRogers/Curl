---
id: BL-2622
title: Make lib591's FTP active-mode upload cases 593 and 594 finish instead of timing out
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2622 — Make lib591's FTP active-mode upload cases 593 and 594 finish instead of timing out

## Goal

Upstream cases 593 and 594 (lib591: an active-mode FTP upload through a multi handle whose server answers the data connection with an error) finish within the case's accept timeout and pass through the runner, as 591 and 592 already do.

## Context

Found by BL-2533: `Lib591` passes 591 and 592 (on the ratchet), but 593 and 594 run about 20 seconds and fail. Read their `<servercmd>` and `<verify>` in `Curl.Conformance.UnitTests/UpstreamTestData`, run each with `CURL_UPSTREAM_CASE=<n>`, and in the first pass find whether the FTP engine's accept wait or the runner's FTP stand-in is at fault; add that project to `touches` before any edit.

## Acceptance criteria

- [x] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [x] Any product fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] No added case's output depends on the platform.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- Cause: the FTP engine, not the runner's stand-in. `FtpSession` waited a hard-coded 60 s for the active-mode data connection, so lib591's `CURLOPT_ACCEPTTIMEOUT_MS` (`%FTPTIME2`, 8 s) never reached it and 593 (`NODATACONN150`, expects exit 12) ran past the runner's 20 s. Fix: `ITransferContext.FtpAcceptTimeout` (default `TransferContext.DefaultFtpAcceptTimeout`, 60 s), set by `TransferContextFactory` from `EasyNetworkOptions.AcceptTimeout`, zero meaning not set as in libcurl; `FtpSession` waits that long. 593 now passes in 8 s and is on the ratchet; its output is FTP protocol lines and exit 12, nothing platform-dependent.
- Touches widened to `Curl.Protocol.Abstractions.UnitLibrary` and `.UnitTests` (the context property and the test forwarder), `Curl.Engine.UnitLibrary` and `.UnitTests`, `Curl.Protocol.Ftp.UnitLibrary` and `.UnitTests`: no task in Doing on origin/work/dark-factory named any of them.
- 594 (`NODATACONN`, no reply to `STOR`, expects exit 28) still times out: Curl's FTP has no server-response timeout at all. Real curl 8.22.0 (tool, `STOR=STALL`) ends after 120 s with exit 12, which does not explain upstream's 28 through the multi interface, so it needs upstream's source read first. Filed as BL-2724 with the measurement.
- Tests: `FtpProtocolHandlerActiveModeTests.ExecuteAsync_AcceptTimeoutSet_ServerNeverConnects_QuitsWithExit12AfterThatTimeout`, `TransferContextFactoryTests.Create_NetworkOptionsAcceptTimeout_IsTheContextsFtpAcceptTimeoutWithZeroAsSixtySeconds`. Measure-CodeQuality for the three libraries: 0 failing members.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 92cbe6ef 4963d26c 21e5ddeb 4272a6b9.
- 2026-10-18: Doing -> Done. lib591's case 593 ends after CURLOPT_ACCEPTTIMEOUT_MS with exit 12 and is on the ratchet; 594 filed as BL-2724
