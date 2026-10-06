---
id: BL-868
title: Fail an HTTP/3 request whose stream cannot be opened with 'cannot open bidi streams' and exit 55
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-839]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-868 — Fail an HTTP/3 request whose stream cannot be opened with 'cannot open bidi streams' and exit 55

## Goal

An HTTP/3 request whose request stream cannot be opened on the QUIC connection fails with exit 55 (`CurlExitCode.SendError`) and `cannot open bidi streams`, as curl 8.21.0 does.

## Context

- `Documentation/Planning/Decisions/ADR-0187-http-3-stream-resets-follow-curl-8-21-0-and-a-refused-stream-is-retried-on-a-new-connection.md` (BL-839) makes `curl-8_21_0` the release Curl's HTTP/3 follows and lists this row: in `lib/vquic/cf-ngtcp2.c` at `curl-8_21_0` (`h3_stream_open`), a failing `ngtcp2_conn_open_bidi_stream` is `failf(data, "cannot open bidi streams")` and `CURLE_SEND_ERROR` (8.18.0 said `can get bidi streams`).
- Today `Curl.Protocol.Http.UnitLibrary/Http3StreamConnection.cs` reports a failed open through `MultiplexedConnectionFailedException`'s exit and message (ADR-0172 section 6). Distinguish a stream that cannot be opened (for example the server's bidirectional stream limit is used up) from a lost connection, which keeps the exception's exit and message; if `IMultiplexedConnection` cannot tell them apart, say so under Notes and add the distinction in the smallest way inside the touched projects, or file the contract change as its own task and depend on it.

## Acceptance criteria

- [x] A test in `Curl.Protocol.Http.UnitTests` over a fake multiplexed connection that refuses to open a stream pins exit 55 and `cannot open bidi streams`.
- [x] A test pins that a connection lost while opening the stream still reports the `MultiplexedConnectionFailedException`'s exit and message.
- [x] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage.

## Notes

- `IMultiplexedConnection` has no dedicated "cannot open" signal, but the exception type already separates the cases: a lost connection throws `MultiplexedConnectionFailedException`, so any other `IOException` from `OpenBidirectionalStreamAsync` now means the stream cannot be opened. `Http3Session.OpenBidirectionalStreamAsync` maps it to exit 55 `cannot open bidi streams`; no contract change needed (ADR-0245).
- `Curl.Quic`'s `QuicConnection` waits for stream credit instead of failing, so only a fake reaches this path today.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0245; no task in Doing names it.
- Tests: `ExecuteAsync_Http3RequestStreamCannotBeOpened_FailsWithExit55AndCannotOpenBidiStreams` and `ExecuteAsync_Http3ConnectionLostOpeningTheRequestStream_FailsWithTheConnectionsExitAndMessage` (the fake gained `BidirectionalOpenException`). Http tests: 1422 passed; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 100% line, 100% branch.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. An HTTP/3 request stream the connection cannot open fails with exit 55 and 'cannot open bidi streams'; a lost connection keeps its own exit and message
