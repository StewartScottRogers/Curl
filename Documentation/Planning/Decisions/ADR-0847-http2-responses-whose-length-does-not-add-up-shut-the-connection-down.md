# ADR-0847 — HTTP/2 responses whose length does not add up shut the connection down

- **Status:** Accepted
- **Date:** 2026-10-26

Decided by Claude under Stewart's delegation (task BL-3667, CGR-138).

## Context

CGR-138 (`Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`) found that curl's nghttp2 layer
checks a response against RFC 9113 section 8.1.1 and Curl's `Http2StreamConnection` did not. BL-3667
measured curl 8.18.0 with nghttp2 1.68.0 (WSL) against a scripted HTTP/2 server (BL-3667 Notes):
each of the responses below makes curl send GOAWAY with PROTOCOL_ERROR, last stream 0 and no debug
data, and exit 16, even with `--ignore-content-length`:

- DATA past the `content-length`: nothing of that DATA is written.
- A stream that ends short of the `content-length`: the DATA before it is written.
- DATA on a response to HEAD (also `-X HEAD`), a 204 or a 304: nothing of it is written.
- A 1xx head that ends the stream: its status and header lines are written, but no empty line.
- A final head that ends the stream although its `content-length` promises a body (added by
  BL-5947, curl 8.18.0): the same, also for `-I -X GET`; `-v` shows the message after the head
  lines. Plain `-I` sends HEAD, which is promised no body, and succeeds.
- A `content-length` that is not a number, or a second one: the lines before it are written, no
  empty line, and the message is `Invalid HTTP header field was received: frame type: 1, stream: 1,
  name: [content-length], value: [abc]`.

Otherwise the message is `nghttp2 shuts down connection with error 1: PROTOCOL_ERROR`.

BL-5958 measured `-v` for these and for the other responses curl shuts the connection down for
(headers after the head without END_STREAM, an invalid header field in a head or a trailer): every
one writes `* nghttp2 shuts down connection with error 1: PROTOCOL_ERROR` before
`* closing connection #0`, after the data lines of the DATA before the failure, and an invalid
field writes its own message as a `-v` line just before it.

## Decision

1. `Http2StreamConnection` keeps the body length the final head promises (none for HEAD, 204 and
   304; else its `content-length`) and counts the DATA it receives. DATA past it, or an END_STREAM
   short of it, shuts the connection down.
2. A shut-down sends the GOAWAY at once and keeps the exit 16 failure, which the stream's next read
   throws after the bytes curl writes before it (the DATA before a short end, a head without its
   empty line), so `-i`, `-D` and the output file get what curl's get.
3. A request upgraded to h2c learns that it is a HEAD from `HttpH2cUpgradeConnection`, which sees the
   HTTP/1.1 request head go out, so a HEAD's `content-length` with no DATA stays a whole response
   (upstream test1702).

## Consequences

The check is in the stream, not in the HTTP/1 body reader, so `--ignore-content-length` does not turn
it off, as it does not in curl. The message for an invalid header field is built in
`Http2StreamConnection`, the only place that gives it.
