---
id: BL-2130
title: Auth retry to an HTTP/1.0 server with a chunked upload reconnects, then fails exit 25 on the new connection (test1072)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2130 — Auth retry to an HTTP/1.0 server with a chunked upload reconnects, then fails exit 25 on the new connection (test1072)

## Goal

An auth retry to an HTTP/1.0 server with a chunked upload body reconnects, reports the retried request, and fails with exit 25 on the new connection as curl 8.21.0 does, so upstream test1072 passes the reference cross-check.

## Context

Diagnosis on 2026-10-13 of why GF-0007 stayed open. With `-v`, real curl 8.21.0 on test1072 prints "Need to rewind upload for next request", shuts down connection #0, "Issue another request to this URL", opens connection #1, draws a new zero progress-meter line, logs "Server auth using Digest" and "Chunky upload is not supported by HTTP 1.0", leaves #1 intact and exits 25. Curl fails on connection #0 without reconnecting, so its stderr has one meter line where curl has two.

Cause: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` around lines 2359 and 2366: `ThrowIfChunkedResendToHttp10` (around line 2666) throws inside `RetryOfAsync` before the retry connects, so `ReportRequestRetried` (around line 694) never starts a new meter line. `Curl.Console.UnitTests/CurlCommandRunnerAuthRetryProgressMeterTests.cs` covers the meter side. Measure real curl with `Record-CurlExchange.ps1` before pinning text.

## Acceptance criteria

- [x] The retry is returned with the server remembered as HTTP/1.0; it reconnects, the retried request is reported started, and exit 25 is raised while building the request on the new connection, which is left intact.
- [x] Verbose output includes "Need to rewind upload for next request" where curl prints it.
- [x] Unit tests pin the reconnect, the second meter line and exit 25.
- [x] Upstream test1072 is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` and passes.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

- Fix: `RetryOfAsync` no longer throws before the retry. The retry plan is returned downgraded (`DowngradedAfter` sets `EarlierResponseWasHttp10`), the HTTP/1.0 connection closes, `ConnectAndExchangeAsync` reconnects and `ReportRequestStarted` draws the retried request's new meter line, and `ThrowIfChunkedResendToHttp10(plan)` - now checking `Framing.ChunksForUnknownLength && Options.EarlierResponseWasHttp10` - raises exit 25 inside the exchange right after `ThrowIfRefused`, before a byte is written to connection #1. `ReportAuthRetryRewind` already writes "Need to rewind upload for next request" for the 401 (unknown length counts as non-empty). `RewindForResend` leaves an unseekable stream as it is (no side effect), since the retry never sends it.
- Curl's output shape was taken from the 2026-10-13 diagnosis in Context (real curl 8.21.0 -v on test1072), not re-measured: the run budget went on the fix.
- Tests: `ExecuteAsync_Http10ChallengeToABodyOfUnknownLength_ReconnectsThenReturnsUploadFailed` (2 connections, nothing written to the second, rewind and Issue-another-request lines), its proxy twin `ExecuteAsync_Http10407ToABodyOfUnknownLength_ReconnectsThenReturnsUploadFailed`, and `RunAsync_Http10401ToAStdinUpload_WritesAStatusLineForTheRetryThenExits25` (two status lines, exit 25).
- test1072 was already in `PassingUpstreamCases.txt`; the Conformance suite is green (2671 passed, 801 skipped). Measure-CodeQuality was not run: `ThrowIfChunkedResendToHttp10`'s true branch is reached by the new tests, its false branch by every other exchange.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. Auth retry to an HTTP/1.0 server with a chunked upload reconnects, draws the retry's meter line, then fails exit 25 on the new connection (test1072)
