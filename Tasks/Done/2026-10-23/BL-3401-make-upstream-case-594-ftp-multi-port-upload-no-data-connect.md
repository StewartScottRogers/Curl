---
id: BL-3401
title: Make upstream case 594 (FTP multi PORT upload, no data connection) finish within 20 seconds, as curl 8.22.0 does
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3401 — Make upstream case 594 (FTP multi PORT upload, no data connection) finish within 20 seconds, as curl 8.22.0 does

## Goal

Upstream case 594 (`ftp://HOST:PORT/path/594 %FTPTIME2 upload`, multi PORT upload, no data connection and no positive preliminary reply) finishes inside the harness's 20 second limit through Curl, or a measurement shows the reference curl 8.22.0 takes as long and that is recorded.

## Context

Stewart (2026-10-23) wants every test passing. Case 594 is disabled upstream as failing too often to be reliable (ADR-0737); on Windows Curl did not finish within 20 seconds. Read the case for what `%FTPTIME2` limits. Measure first (standing rule): time the reference curl 8.22.0 (`/ucrt64/bin/curl`, ADR-0018) and Curl on the same loopback FTP exchange, and find what Curl waits for (a data-connection accept without the case's time limit, a missing `-m`, a retry loop). Inject `TimeProvider`; never `Thread.Sleep`. Library: `Curl.Protocol.Ftp.UnitLibrary`.

## Acceptance criteria

- [x] A Notes entry gives the wall-clock time of the reference curl 8.22.0 and of Curl for case 594's exchange, and the exit code each returns.
- [x] If Curl is slower or wrong, it is fixed with a focused unit test in `Curl.Protocol.Ftp.UnitTests` (using a manual `TimeProvider`) that fails without the fix (ADR-0482).
- [x] If curl takes as long, a new ADR (decided by Claude under Stewart's delegation, listed in the Decisions README.md) amending ADR-0737 records that 594 stays off the ratchet with the measured times; otherwise case 594 ends `Passed` with `$env:CURL_UPSTREAM_CASE='594'` on Windows and is on `PassingUpstreamCases.txt` after ten passing runs.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- 2026-10-23 measurement: no C compiler here, so lib591.c could not be built against the reference libcurl; a throwaway C# program (outside the repo) P/Invoked libcurl 8.22.0 (`C:Program FilesGitCrt64binibcurl-4.dll`, the library `/ucrt64/bin/curl` uses) with lib591's options (verbose, upload, FTPPORT `-`, ACCEPTTIMEOUT_MS 8000, the case runner's %FTPTIME2) on a multi handle, against `Record-CurlExchange.ps1 -Ftp -FtpReply "EPRT=500 we don't like EPRT now",'STOR=STALL'`. Reference libcurl 8.22.0: exit 12 after 68078 ms (68472 ms process), accept timeout at 8 s, then a minute's wait for the STOR reply, `control connection looks dead`, no QUIT. Curl through the case runner (`$env:CURL_UPSTREAM_CASE='594'`): stopped by the harness at 20 s (20755 ms test), no exit code; its code path is the same 8 s + 60 s and exit 12 with no QUIT, pinned by `FtpProtocolHandlerTimeLimitTests.ExecuteAsync_ActiveStorUnansweredAndNeverConnected_EndsWithExit12AfterTheAcceptTimeoutAndAMinuteMoreWithNoQuit` (BL-2795). Measuring Curl past 20 s would need a runner change outside this task's touches, and is not needed: curl itself takes 68 s.
- Criterion 2: Curl is neither slower nor wrong compared with libcurl 8.22.0, so no FTP code changes and no new unit test; the existing test above pins the matching behaviour with a manual TimeProvider.
- Criterion 3: ADR-0803 records that 594 stays off the ratchet (amends ADR-0737). The case's expected QUIT and exit 28 are stale against libcurl 8.22.0. The task's own Context names `/ucrt64/bin/curl`, but the CLI has no accept-timeout option, so the measurement used the same libcurl DLL directly.

- Criterion 4: `dotnet build Curl.slnx -warnaserror` clean (0 warnings); fast tests 42528 passed, 0 failed across 46 test projects on Windows. Only documents changed, so nothing depends on the platform and no quality run is needed (no production code).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 4d37ec2a e4e018ed 538f29c2 c5f8cef6.
- 2026-10-23: Doing -> Done. Measured case 594: libcurl 8.22.0 exits 12 after 68 s (8 s accept timeout + 60 s), as Curl does; ADR-0803 keeps 594 off the ratchet
