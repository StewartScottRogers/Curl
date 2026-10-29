# ADR-1063 — An HTTP/3 client session offers server-opened streams of unknown types to an opt-in callback

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4223.

## Context

WebTransport over HTTP/3 (draft-ietf-webtrans-http3 sections 4.2 and 4.3, BL-4113) needs the server-opened streams
HTTP/3 itself does not use: unidirectional streams of type `0x54` and bidirectional streams that start with the
`0x41` signal. `Http3ClientSession` stopped every unknown unidirectional stream with `H3_STREAM_CREATION_ERROR`, as
RFC 9114 section 6.2 allows, and `IMultiplexedConnection` had no way to accept a peer bidirectional stream.

## Decision

- **A new optional interface, `IPeerBidirectionalStreamAccepting`**, in `Curl.Protocol.Abstractions.UnitLibrary`, with
  `AcceptBidirectionalStreamAsync`, as `IDatagramCarryingConnection` is for datagrams: `QuicConnection` implements it
  over `QuicStreamSet.AcceptBidirectional`, and no other `IMultiplexedConnection` has to change.
- **`Http3ClientSession` takes an optional `Func<Http3UnknownPeerStream, bool>`** (constructor parameter). It is called
  with each unidirectional stream of an unknown or grease type, after its type, and, when the connection implements
  the new interface, each bidirectional stream, after its first variable-length integer. It returns whether it took
  the stream; one it declines, or that ends before its first integer, is stopped with `H3_STREAM_CREATION_ERROR`. The
  session disposes every such stream with itself. Without the callback nothing changes: unknown unidirectional
  streams are stopped as before, and the session never asks the connection for a bidirectional stream.
- **`Http3PeerUnidirectionalStreams.ReadTypeAsync`** gives a stream's type as its number, with the same checks as
  `AcceptAsync` (a second critical stream, a push stream), so the session can hand the unknown type on;
  `AcceptAsync` keeps its contract for `Curl.Protocol.Http`'s `Http3Session`.
- **The QUIC transport parameters do not change.** The client keeps declaring curl's ngtcp2
  `initial_max_streams_bidi` of 262144 (ADR-0144 section 5), so the Initial stays byte for byte curl's. The opt-in is
  at the HTTP/3 layer: without the callback no peer bidirectional stream is accepted or read, as before, and with it
  the streams the server was always allowed to open now reach the caller.

## Consequences

- An extension gets WebTransport's streams from `Http3ClientSession` with no change to curl's own HTTP/3 path.
- `Curl.Engine`'s `SocketCallbackMultiplexedConnection` does not forward the new interface yet, so a session over a
  connection wrapped for socket callbacks accepts no bidirectional streams; a caller that needs them wraps nothing or
  forwards the interface.
