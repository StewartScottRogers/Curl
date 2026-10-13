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
completed:
---
# BL-2130 — Auth retry to an HTTP/1.0 server with a chunked upload reconnects, then fails exit 25 on the new connection (test1072)

## Goal

An auth retry to an HTTP/1.0 server with a chunked upload body reconnects, reports the retried request, and fails with exit 25 on the new connection as curl 8.21.0 does, so upstream test1072 passes the reference cross-check.

## Context

Diagnosis on 2026-10-13 of why GF-0007 stayed open. With `-v`, real curl 8.21.0 on test1072 prints "Need to rewind upload for next request", shuts down connection #0, "Issue another request to this URL", opens connection #1, draws a new zero progress-meter line, logs "Server auth using Digest" and "Chunky upload is not supported by HTTP 1.0", leaves #1 intact and exits 25. Curl fails on connection #0 without reconnecting, so its stderr has one meter line where curl has two.

Cause: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` around lines 2359 and 2366: `ThrowIfChunkedResendToHttp10` (around line 2666) throws inside `RetryOfAsync` before the retry connects, so `ReportRequestRetried` (around line 694) never starts a new meter line. `Curl.Console.UnitTests/CurlCommandRunnerAuthRetryProgressMeterTests.cs` covers the meter side. Measure real curl with `Record-CurlExchange.ps1` before pinning text.

## Acceptance criteria

- [ ] The retry is returned with the server remembered as HTTP/1.0; it reconnects, the retried request is reported started, and exit 25 is raised while building the request on the new connection, which is left intact.
- [ ] Verbose output includes "Need to rewind upload for next request" where curl prints it.
- [ ] Unit tests pin the reconnect, the second meter line and exit 25.
- [ ] Upstream test1072 is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` and passes.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

## Log

- 2026-10-13: Created.
