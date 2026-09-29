---
id: BL-830
title: Read the server's HTTP/3 control and QPACK streams
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-731]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-830 — Read the server's HTTP/3 control and QPACK streams

## Goal

`Http3Session` accepts the server's unidirectional streams for the life of the connection and reads them with `Curl.Http3.UnitLibrary`'s readers, so a server `GOAWAY` stops new requests on the connection (`AcceptsNewStreams` turns false), the server's `SETTINGS` are checked, its QPACK encoder and decoder streams are fed to the session's `QpackDecoder`/`QpackEncoder`, and a critical-stream violation fails the transfer with exit 56 and curl's message.

## Context

- Today `Curl.Protocol.Http.UnitLibrary/Http3Session.cs` never calls `IMultiplexedConnection.AcceptUnidirectionalStreamAsync` (`Curl.Protocol.Abstractions.UnitLibrary/IMultiplexedConnection.cs`), and `AcceptsNewStreams` is always `true`. BL-731 left this out on purpose (ADR-0172, `Documentation/Planning/Decisions/ADR-0172-http-3-requests-run-on-an-http3session-and-fall-back-to-tcp-only-when-quic-fails.md`, written by BL-731). `HttpProtocolHandler` already consults `IHttpStreamSession.AcceptsNewStreams` when deciding whether a pooled connection takes another request (around `HttpProtocolHandler.cs` line 805); `Http2Session.AcceptsNewStreams` (`PeerGoAway is null && !IsClosedByPeer`) is the model.
- Building blocks, all in `Curl.Http3.UnitLibrary` (BL-730, not to be changed here): `Http3PeerUnidirectionalStreams.AcceptAsync(Stream, CancellationToken)` reads a peer stream's type and enforces one control, one QPACK encoder and one QPACK decoder stream; `Http3ControlStreamReader` (`PeerSettings`, `GoawayStreamId`, `ReadFrameAsync`, throwing `Http3Exception` with `Http3ErrorCode.MissingSettings`, `ClosedCriticalStream`, `IdError` and the like); `Http3PeerQpackStreams.ReadEncoderStreamAsync(Stream, QpackDecoder, Memory<byte>, CancellationToken)` and `ReadDecoderStreamAsync(Stream, QpackEncoder, …)`. Wrap each `IMultiplexedStream` in the existing `MultiplexedStreamAdapter` to get a `Stream`.
- Exit codes and messages: ADR-0144 section 7 (`Documentation/Planning/Decisions/ADR-0144-http-3-is-hand-built-over-a-hand-built-quic-and-http3-races-tcp-as-curls-ngtcp2-build-does.md`), from `lib/vquic/curl_ngtcp2.c` at curl tag `curl-8_18_0` (the source of curl.se's 8.18.0 Windows build, the measured HTTP/3 reference). There, an error from `nghttp3_conn_read_stream` on any stream is fatal to the connection (`cf_ngtcp2_h3_err_is_fatal` treats `NGHTTP3_ERR_H3_CLOSED_CRITICAL_STREAM` and everything at or below `NGHTTP3_ERR_FATAL` as fatal). ADR-0172 pins the transfer's failure for an HTTP/3 connection error as exit 56 `RecvError` with `nghttp3_conn_read_stream returned error: <nghttp3 name>`, which `HttpTransferMessages.Http3ReadStreamFailed(errorName)` already formats and `Http3StreamConnection` already raises for request-stream errors (reuse its `Nghttp3ErrorName` mapping; e.g. `ERR_H3_CLOSED_CRITICAL_STREAM`, `ERR_H3_MISSING_SETTINGS`, `ERR_H3_FRAME_UNEXPECTED`, `ERR_H3_ID_ERROR`, `ERR_H3_SETTINGS_ERROR`, as nghttp3 1.15's `nghttp3_strerror` names them).
- A connection error found on a peer stream must reach the transfer currently reading from the session (fail its next read with the exit-56 error) and close the connection with the matching HTTP/3 application error code; a `GOAWAY` alone does not fail the in-flight request whose stream ID is below the GOAWAY ID.
- The accept loop runs in the background for the session's lifetime, is started when the session is created, observes the session's cancellation, and ends when the session is disposed; no `Thread.Sleep`, no unobserved task exceptions.

## Acceptance criteria

- [ ] `Curl.Protocol.Http.UnitTests/Http3SessionTests.cs` has tests, over the existing `FakeMultiplexedConnection` and `FakeMultiplexedStream`, that pin: `AcceptsNewStreams` is true before any `GOAWAY` and false after the server's control stream delivers `SETTINGS` then `GOAWAY`; the server's `SETTINGS` are available on the session after they arrive; a control stream whose first frame is not `SETTINGS` fails the next read with exit 56 and `nghttp3_conn_read_stream returned error: ERR_H3_MISSING_SETTINGS`; the server closing its control stream fails it with exit 56 and `…: ERR_H3_CLOSED_CRITICAL_STREAM`; a second control stream fails it with exit 56 and `…: ERR_H3_STREAM_CREATION_ERROR`, and a push stream with exit 56 and `…: ERR_H3_ID_ERROR`; disposing the session ends the accept loop.
- [ ] `Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Http3.cs` has a test showing a second request to the same origin after a `GOAWAY` opens a new QUIC connection instead of reusing the first.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes; no new test needs `TestCategory=Integration`.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
