# ADR-0836 — HTTP/2 against a peer that answers in HTTP/1.1 exits 16

- **Status:** Accepted
- **Date:** 2026-10-26

Decided by Claude under Stewart's delegation (task BL-3650, CGR-121).

## Context

BL-3372 made `--http2-prior-knowledge` against an HTTP/1.1 server exit 55 with nghttp2's text
`Remote peer returned unexpected data while we expected SETTINGS frame.  Perhaps, peer does not
support HTTP/2 properly.`, from a measurement whose server answered the preface with
`HTTP/1.1 400` and `Connection: close` and then closed. Gap report issue CGR-121 measured exit 16
for the same text, and upstream 8.22.0 test1801 (an h2c upgrade answered by `101` and then
HTTP/1.1) expects 16.

BL-3650 measured curl 8.18.0 (nghttp2 1.68.0, WSL Ubuntu; no 8.22.0 nghttp2 build is at hand) on
2026-10-26 against `nc` servers:

- a server that sends `HTTP/1.1 200 OK` and keeps the connection open, at once or after reading
  the request: exit 16 in all six runs; the `--http2` upgrade answered by `101` and then HTTP/1.1
  (test1801's server): exit 16;
- a server that sends `HTTP/1.1 400 ... Connection: close` and closes: exit 16 in three runs and
  55 in four, the same text; which one depends on whether curl's next send meets the closed socket;
- a server that closes without replying: `Send failure: Broken pipe`, exit 55 or 16 by the same race.

## Decision

`Http2Session.ReadAndRouteFrameAsync` maps a FRAME_SIZE_ERROR before the peer's SETTINGS (an
HTTP/1.1 reply read as a frame header) to exit 16 (`CurlExitCode.Http2`) with nghttp2's text, for
prior knowledge over cleartext or TLS and for the h2c upgrade. Exit 16 is what curl gives every
time the server keeps the connection open and what upstream's own test expects; exit 55 is one
side of a race against a closing server, which no single answer can match every run, so it is
not reproduced.

## Consequences

- `HttpProtocolHandlerTests.ExecuteAsync_Http2PriorKnowledgeAgainstHttp11Server_FailsWithExit16WhetherOrNotItResets`
  and `Cgr121Tests` pin exit 16.
- A server that closes without replying still gives Curl's `Send failure` exit 55, one of curl's
  two racy answers.
