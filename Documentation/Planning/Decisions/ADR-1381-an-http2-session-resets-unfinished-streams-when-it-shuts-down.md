# ADR-1381 — An HTTP/2 session resets its unfinished streams when it shuts down

- **Status:** Accepted
- **Date:** 2026-11-24

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-5922.

## Context

curl resets the stream of a transfer that ends before its stream does: RST_STREAM with
STREAM_CLOSED, then, when the connection closes, GOAWAY. Curl sent only the GOAWAY. Measured
with curl 8.18.0 (nghttp2): `-m` passing while the response is unfinished, and `-f` on a
`404` whose body has not come, send both frames; `-I` answered with a head and a DATA frame
sends no RST_STREAM.

## Decision

`Http2Session.ShutDownAsync` resets every stream whose response is unfinished, lowest first,
before its GOAWAY. A stream is unfinished until its END_STREAM or the server's RST_STREAM has
come, or `-I` has ended it at a DATA frame. Whatever skips the GOAWAY (no preface, a GOAWAY
already sent, a connection the peer closed or the handler gave up on) skips the resets too.
A timed-out transfer on an h2c-upgraded connection now hands its session to the connection,
so it is shut down the same way.

## Why

Every transfer that ends early on a connection of its own closes that connection, so one
place covers `-m`, `-f` and the rest with curl's bytes. On a connection `-Z` transfers still
share, the reset goes out when the connection closes rather than when the one transfer ends;
nothing measured depends on that difference.
