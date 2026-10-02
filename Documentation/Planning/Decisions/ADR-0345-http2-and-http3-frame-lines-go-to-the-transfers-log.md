# ADR-0345 — HTTP/2 and HTTP/3 frame lines go to the log of the transfer they belong to

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1073.

## Context

BL-922 (ADR-0222) logs each HTTP exchange's steps to Curl's own diagnostic log but no
HTTP/2 or HTTP/3 frame. An `Http2Session` outlives one transfer when its connection is
pooled (ADR-0159 point 5) and carries several `-Z` transfers at once (BL-717), each with its
own `ITransferContext.DiagnosticLog`, so some frames belong to no one transfer: the server's
SETTINGS and GOAWAY are about the connection. `Curl.Http2` and `Curl.Http3` have no
reference to the abstractions, and `Http2Connection` applies SETTINGS, GOAWAY, PING and
WINDOW_UPDATE itself, returning only HEADERS and DATA to its caller.

## Decision

1. `IHttpStreamSession.CreateStream` takes the transfer's `IDiagnosticLog`; each
   `Http2StreamConnection` and `Http3StreamConnection` writes through an `HttpFrameLog` over
   it, under component `http2` or `http3`.
2. A frame of one stream is logged to that stream's transfer: HEADERS, DATA and RST_STREAM
   sent, HEADERS and DATA received at `verbose` (`HEADERS sent on stream 1, 23 bytes`), and
   the server's RST_STREAM (HTTP/3: the QUIC stream reset) at `warning` with its error code.
   A frame is named by type, stream and payload length only, so no header value is logged.
3. The connection's own frames - the server's first SETTINGS at `info`, its GOAWAY at
   `warning` with its last stream and error code - go to the log of the transfer that last
   opened a stream on the session. That is the transfer relying on the connection now; a
   pooled session hands its log on to each transfer it carries, and none is kept past it.
4. Because `Http2Connection` handles SETTINGS and GOAWAY inside its read, the session sees
   them as a change of its state (`IsPeerSettingsReceived`, `PeerGoAway`) across one read,
   whether that read returned or threw. A later SETTINGS that changes values is not logged.
5. HTTP/3's SETTINGS and GOAWAY arrive on the control stream, read in the background by
   `Http3Session` with no transfer at hand; they are not logged yet.

## Consequences

- `HttpProtocolHandlerTests.FrameLog.cs` and `HttpFrameLogTests` pin the lines; with
  `NoDiagnosticLog.Instance` or no log, `HttpFrameLog.Silent` writes nothing and every
  earlier test is unchanged.
- Connection-level lines of a session shared by `-Z` transfers land in one of their logs,
  not all; the run's log is one file in practice, so nothing is lost.
