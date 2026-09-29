---
id: BL-834
title: Retry an HTTP/3 request the server refused with H3_REQUEST_REJECTED
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-731, BL-839]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-834 — Retry an HTTP/3 request the server refused with H3_REQUEST_REJECTED

## Goal

An HTTP/3 request stream the server resets behaves as BL-839's ADR decides: if it follows curl 8.19.0 or later, a reset with `H3_REQUEST_REJECTED` (0x10b) prints `HTTP/3 stream <id> refused by server, try again on a new connection`, stops the connection taking new streams and retries the request on a new connection as often as the ADR states, and other resets print that release's text; if it keeps curl 8.18.0, tests pin that a 0x10b reset is reported like any other reset and the task closes saying so.

## Context

- Today `Curl.Protocol.Http.UnitLibrary/Http3StreamConnection.cs` reports every reset as exit 95 (`CurlExitCode.Http3`) or 18 (`CurlExitCode.PartialFile`, once body bytes arrived) with `HttpTransferMessages.Http3StreamReset(streamId)` = `HTTP/3 stream <id> reset by server` (BL-731, `Documentation/Planning/Decisions/ADR-0172-http-3-requests-run-on-an-http3session-and-fall-back-to-tcp-only-when-quic-fails.md`). That matches `lib/vquic/curl_ngtcp2.c` at `curl-8_18_0`, the measured HTTP/3 reference build, which has no `H3_REQUEST_REJECTED` case; curl 8.19.0 and later add the refused-stream retry and change the reset text to `HTTP/3 stream <id> reset by server (error 0x<hex> <name>)`. BL-839 decides which Curl follows and names its ADR in its Notes; read that ADR first and implement it exactly.
- The error code is `Http3ErrorCode.RequestRejected` (0x010b) in `Curl.Http3.UnitLibrary/Http3ErrorCode.cs`; the reset's application error code comes from the `IMultiplexedStream` read failure (see how `Http3StreamConnection` detects a reset today).
- curl's retry is the model: from 8.19.0 `recv_closed_stream` sets `data->state.refused_stream` and returns `CURLE_RECV_ERROR`, and `Curl_retry_request` (`lib/transfer.c`) re-sends on a fresh connection without `--retry`; `lib/http2.c` does the same for `NGHTTP2_REFUSED_STREAM`. If `Curl.Protocol.Http.UnitLibrary` already retries a refused HTTP/2 stream, reuse that path; otherwise add the retry in `HttpProtocolHandler` where a request's outcome is examined (the loop that sends "each retry that may go on the same connection"), and make `Http3Session.AcceptsNewStreams` false for the refusing connection so it is not reused.
- The info line goes out through `plan.Context.Events.ReportInfo`, printed as a `*` line under `-v`, as curl's `infof` is.

## Acceptance criteria

- [x] `Curl.Protocol.Http.UnitTests/Http3StreamConnectionTests.cs` and `Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Http3.cs` have tests, over fake multiplexed connections, pinning what BL-839's ADR states: for a reset with 0x10b, the exit code, the `-v` info or error line, and whether a second QUIC connection is dialled and its response delivered with exit 0; for a reset with another code, before and after body bytes, exit 95 and 18 with the ADR's exact text; and, if the ADR adopts it, that `-I` ignores a reset after complete response headers.
- [x] If the ADR adopts the retry, a test shows a server that refuses every attempt ends with the ADR's exit code and message after the stated number of attempts rather than looping.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes; no new test needs `TestCategory=Integration`.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- BL-839 decided: ADR-0187 (`Documentation/Planning/Decisions/ADR-0187-http-3-stream-resets-follow-curl-8-21-0-and-a-refused-stream-is-retried-on-a-new-connection.md`) follows `curl-8_21_0` - retry up to 5 times, sixth refusal exit 56 `Connection died, tried 5 times before giving up`; reset text `(error 0x<hex> <name>)`; `-I` exemption. The `:status` abort (H3_MESSAGE_ERROR) then reads `... (error 0x10e MESSAGE_ERROR)`.
- Built (2026-09-29): `Http3StreamConnection` reads the reset's code. `0x10b` calls `Http3Session.StopNewStreams` (so `AcceptsNewStreams` goes false) and throws exit 56 marked `HttpTransferException.IsStreamRefused`, whose message is the refused `-v` line; `0x100` (`H3_NO_ERROR`), or any other code after the final head when `-I` wants no body (new `ignoresBody` argument of `IHttpStreamSession.CreateStream`, unused by HTTP/2), ends the stream as its FIN would; every other code is `reset by server (error 0x<hex> <name>)`, names from `vquic_h3_err_str` in `HttpTransferMessages.Http3ErrorName`, greasing codes `NO_ERROR`.
- The retry reuses the handler's died-connection path: `HttpProtocolHandler.StreamRefusedOutcome` reports `REFUSED_STREAM, retrying a fresh connect`, then `ExchangeOnConnectionAsync` reports `Connection died, retrying a fresh connect (retry count: n)` (now formatted, `HttpConnectionInfoLines.ConnectionDiedRetrying(int)`), then `shutting down connection #N` and `Issue another request to this URL`, and `ConnectAndExchangeAsync` connects again as it did first. The count is `HttpRequestPlan.StreamRefusedRetries`, capped by `HttpProtocolHandler.MaximumStreamRefusedRetries` = 5; the sixth refusal fails exit 56 `Connection died, tried 5 times before giving up`. A refusal after response bytes arrived fails exit 56 `Failure when receiving data from the peer` with no retry.
- Choices (defaults taken): the refused-retry count lives on the request plan, so it is per handler call - curl shares it across redirects and with the pooled-connection retry, but HTTP/3 connections are never pooled and a redirect is a new handler call, so the two can only differ on a redirect chain that is refused more than five times in total. A `-T` body read from a stream is not sent again (as no retry in this handler does) and fails like a begun response; filed as BL-885.
- Found: `Documentation/Planning/Decisions` holds two ADR-0187s (and two ADR-0193s) from parallel lanes; filed as BL-886.
- Tests: 5 new handler tests (retry then success with the full `-v` sequence and the body resent; refused six times; refused after the head and with a stream body; `-I` reset after the head with `0x10c` and `0x100`; `0x100` before the head) and 4 new stream tests (refused marks the session; 10 codes named; `-I` reset after/before the head). `Curl.Protocol.Http.UnitTests` 1316 passing; the quality gate 100% line, 100% branch, 0 failing members (`ExchangeAsync` kept at complexity 10 by folding the refused case into `FailedOutcome`).

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. A server's H3_REQUEST_REJECTED is retried on a new QUIC connection up to 5 times as curl 8.21.0 does, and every other HTTP/3 reset names its error code
