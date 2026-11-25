# ADR-1357 — An ignored HTTP/2 body is read to its end, except on an upgraded or unsent stream

- **Status:** Accepted
- **Date:** 2026-11-24

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-5906.
Supersedes the part of [ADR-0343](ADR-0343-an-ignored-http2-body-resets-its-stream.md) that
applies the reset to every HTTP/2 stream.

## Context

ADR-0343 reset every HTTP/2 stream whose body is ignored (a `401` answered with a retry, a
followed redirect) with STREAM_CLOSED, on the strength of one measurement after an h2c
upgrade. Surl's port comparison saw that upstream curl 8.21.0 sends no `RST_STREAM` over
prior knowledge. BL-5906 measured curl 8.18.0 with nghttp2:

| Case | Frames curl sends for stream 1 |
| --- | --- |
| prior knowledge, Digest `401`, DATA with END_STREAM | no reset; HEADERS on stream 3 |
| prior knowledge, `302` with `-L`, DATA with END_STREAM | no reset; HEADERS on stream 3 |
| prior knowledge, `401` or `302`, stream never ended | waits for the body; RST_STREAM STREAM_CLOSED only when `-m` gives up |
| h2c upgrade, Digest `401`, DATA with END_STREAM | RST_STREAM STREAM_CLOSED, then HEADERS on stream 3 |

## Decision

`Http2StreamConnection.AbandonResponseAsync` reads a stream whose request has ended to its
end, drops its DATA and sends no reset. The stream an h2c upgrade continues on keeps
ADR-0343's behaviour: it is reset with STREAM_CLOSED unless its response has ended, as
measured. A stream whose request has not ended (a response that arrived before the upload
finished) also keeps it: that case is not measured, and reading it could wait for a server
that is waiting for the rest of the request.

## Consequences

- A retry or redirect over HTTP/2 waits for the ignored body, as curl's does; a server that
  never ends the stream makes the transfer wait for its time limit.
- A connection closed or a stream reset while the ignored body is read fails the transfer.

## Alternatives considered

- Take only the frames already received, then reset: curl does not reset a stream that is
  merely unfinished, it waits for it.
- Drop the upgraded stream's reset too: contradicts both measurements of the upgrade.
