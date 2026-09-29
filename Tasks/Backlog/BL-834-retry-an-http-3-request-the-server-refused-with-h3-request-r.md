---
id: BL-834
title: Retry an HTTP/3 request the server refused with H3_REQUEST_REJECTED
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-731, BL-833]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-834 — Retry an HTTP/3 request the server refused with H3_REQUEST_REJECTED

## Goal

An HTTP/3 request stream the server resets behaves as BL-833's ADR decides: if it follows curl 8.19.0 or later, a reset with `H3_REQUEST_REJECTED` (0x10b) prints `HTTP/3 stream <id> refused by server, try again on a new connection`, stops the connection taking new streams and retries the request on a new connection as often as the ADR states, and other resets print that release's text; if it keeps curl 8.18.0, tests pin that a 0x10b reset is reported like any other reset and the task closes saying so.

## Context

- Today `Curl.Protocol.Http.UnitLibrary/Http3StreamConnection.cs` reports every reset as exit 95 (`CurlExitCode.Http3`) or 18 (`CurlExitCode.PartialFile`, once body bytes arrived) with `HttpTransferMessages.Http3StreamReset(streamId)` = `HTTP/3 stream <id> reset by server` (BL-731, `Documentation/Planning/Decisions/ADR-0169-http-3-requests-run-on-an-http3session-and-fall-back-to-tcp-only-when-quic-fails.md`). That matches `lib/vquic/curl_ngtcp2.c` at `curl-8_18_0`, the measured HTTP/3 reference build, which has no `H3_REQUEST_REJECTED` case; curl 8.19.0 and later add the refused-stream retry and change the reset text to `HTTP/3 stream <id> reset by server (error 0x<hex> <name>)`. BL-833 decides which Curl follows and names its ADR in its Notes; read that ADR first and implement it exactly.
- The error code is `Http3ErrorCode.RequestRejected` (0x010b) in `Curl.Http3.UnitLibrary/Http3ErrorCode.cs`; the reset's application error code comes from the `IMultiplexedStream` read failure (see how `Http3StreamConnection` detects a reset today).
- curl's retry is the model: from 8.19.0 `recv_closed_stream` sets `data->state.refused_stream` and returns `CURLE_RECV_ERROR`, and `Curl_retry_request` (`lib/transfer.c`) re-sends on a fresh connection without `--retry`; `lib/http2.c` does the same for `NGHTTP2_REFUSED_STREAM`. If `Curl.Protocol.Http.UnitLibrary` already retries a refused HTTP/2 stream, reuse that path; otherwise add the retry in `HttpProtocolHandler` where a request's outcome is examined (the loop that sends "each retry that may go on the same connection"), and make `Http3Session.AcceptsNewStreams` false for the refusing connection so it is not reused.
- The info line goes out through `plan.Context.Events.ReportInfo`, printed as a `*` line under `-v`, as curl's `infof` is.

## Acceptance criteria

- [ ] `Curl.Protocol.Http.UnitTests/Http3StreamConnectionTests.cs` and `Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Http3.cs` have tests, over fake multiplexed connections, pinning what BL-833's ADR states: for a reset with 0x10b, the exit code, the `-v` info or error line, and whether a second QUIC connection is dialled and its response delivered with exit 0; for a reset with another code, before and after body bytes, exit 95 and 18 with the ADR's exact text; and, if the ADR adopts it, that `-I` ignores a reset after complete response headers.
- [ ] If the ADR adopts the retry, a test shows a server that refuses every attempt ends with the ADR's exit code and message after the stated number of attempts rather than looping.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes; no new test needs `TestCategory=Integration`.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
