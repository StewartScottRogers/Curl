# ADR-0245 — An HTTP/3 request stream that cannot be opened is exit 55

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-868.

## Context

ADR-0187 makes curl 8.21.0 the release Curl's HTTP/3 follows. In `lib/vquic/cf-ngtcp2.c`
at `curl-8_21_0`, `h3_stream_open` reports a failing `ngtcp2_conn_open_bidi_stream` (for
example, the server's bidirectional stream limit is used up) with
`failf(data, "cannot open bidi streams")` and `CURLE_SEND_ERROR`, exit 55.

`IMultiplexedConnection.OpenBidirectionalStreamAsync` documented no failures. A lost
connection throws `MultiplexedConnectionFailedException`, an `IOException` carrying curl's
exit code and message (ADR-0144 section 7, ADR-0172 section 6), and the HTTP handler
reported every failure to open a request stream that way.

## Decision

1. Any other `IOException` from `OpenBidirectionalStreamAsync` means the connection is up
   but the stream cannot be opened. `Http3Session` reports it as exit 55 with
   `cannot open bidi streams`.
2. `MultiplexedConnectionFailedException` keeps its own exit code and message, as before.
3. The distinction is made inside `Curl.Protocol.Http.UnitLibrary`, with no change to the
   `Curl.Protocol.Abstractions` contract: the exception type already tells the two cases
   apart.
4. `Curl.Quic`'s `QuicConnection` waits for stream credit rather than failing, as ngtcp2
   does not, so today only a fake connection takes this path. An implementation that
   cannot open a stream throws an `IOException`.

## Consequences

- A reused HTTP/3 connection that cannot open a stream fails with exit 55, which the
  handler's "died before response" rule may send again once on a fresh connection, as it
  does for any exit 55 on a reused connection.
- Tests: `HttpProtocolHandlerTests.ExecuteAsync_Http3RequestStreamCannotBeOpened_FailsWithExit55AndCannotOpenBidiStreams`
  and `ExecuteAsync_Http3ConnectionLostOpeningTheRequestStream_FailsWithTheConnectionsExitAndMessage`.
