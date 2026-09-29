---
id: BL-868
title: Fail an HTTP/3 request whose stream cannot be opened with 'cannot open bidi streams' and exit 55
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-839]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-868 — Fail an HTTP/3 request whose stream cannot be opened with 'cannot open bidi streams' and exit 55

## Goal

An HTTP/3 request whose request stream cannot be opened on the QUIC connection fails with exit 55 (`CurlExitCode.SendError`) and `cannot open bidi streams`, as curl 8.21.0 does.

## Context

- `Documentation/Planning/Decisions/ADR-0187-http-3-stream-resets-follow-curl-8-21-0-and-a-refused-stream-is-retried-on-a-new-connection.md` (BL-839) makes `curl-8_21_0` the release Curl's HTTP/3 follows and lists this row: in `lib/vquic/cf-ngtcp2.c` at `curl-8_21_0` (`h3_stream_open`), a failing `ngtcp2_conn_open_bidi_stream` is `failf(data, "cannot open bidi streams")` and `CURLE_SEND_ERROR` (8.18.0 said `can get bidi streams`).
- Today `Curl.Protocol.Http.UnitLibrary/Http3StreamConnection.cs` reports a failed open through `MultiplexedConnectionFailedException`'s exit and message (ADR-0172 section 6). Distinguish a stream that cannot be opened (for example the server's bidirectional stream limit is used up) from a lost connection, which keeps the exception's exit and message; if `IMultiplexedConnection` cannot tell them apart, say so under Notes and add the distinction in the smallest way inside the touched projects, or file the contract change as its own task and depend on it.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Http.UnitTests` over a fake multiplexed connection that refuses to open a stream pins exit 55 and `cannot open bidi streams`.
- [ ] A test pins that a connection lost while opening the stream still reports the `MultiplexedConnectionFailedException`'s exit and message.
- [ ] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage.

## Notes

## Log

- 2026-09-29: Created.
