# ADR-0343 — An ignored HTTP/2 body resets its stream instead of being read

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-970.

## Context

Over HTTP/1.1 curl reads a body it ignores (a `401` answered with a retry, a redirect `-L`
follows) so the connection can carry the next request. Over HTTP/2 it has no need to: the
stream can simply be closed. BL-970 measured curl.se's nghttp2 curl 8.18.0 after an h2c
upgrade: with `--anyauth -u a:b`, a `401` on stream 1 and its 3-byte DATA with END_STREAM
in the same read, curl prints `Ignoring the response-body` and then sends
`RST_STREAM` on stream 1 with STREAM_CLOSED (`000004 03 00 00000001 00000005`) before the
retry's frames. With a `200` whose body is delivered it sends none. curl's `http2.c` resets
every stream a transfer is done with and that has not closed, with STREAM_CLOSED, and the
same code serves prior-knowledge and ALPN connections.

Curl read the ignored body to its end on HTTP/2 too, so it never sent the reset, and a
server that held the body back would have stalled the retry.

## Decision

On an HTTP/2 stream, a body the handler discards is not read:
`Http2StreamConnection.AbandonResponseAsync` resets the stream with STREAM_CLOSED unless the
frames read so far already ended it, and the frame layer drops what the peer still sends on
it. This holds for every HTTP/2 connection, not only an upgraded one, since curl's code
does not tell them apart. HTTP/3 keeps reading the discarded body until it is measured.

The post-upgrade SETTINGS (INITIAL_WINDOW_SIZE 65536) that curl sends once, right before
the first stream it opens after the upgrade, is written by `Http2Session` straight to the
connection; with one URL and no retry it is never sent, as measured.

## Consequences

- The written bytes after an upgraded `401` retried on stream 3 match curl's byte for byte,
  pinned in `HttpProtocolHandlerTests.H2cUpgrade.cs`.
- A redirect or retry over HTTP/2 no longer waits for the discarded body to arrive.

## Alternatives considered

- Read the body, then send the reset anyway: matches the bytes but keeps the stall and
  resets a stream the peer has already closed.
- Reset only on upgraded connections: no basis in curl's code, and two code paths for one
  behaviour.
