# ADR-1384 — DATA past the length on an unwanted HTTP/2 body closes the connection without failing

- **Status:** Accepted
- **Date:** 2026-11-25
- **Task:** BL-5938

Decided by Claude under Stewart's delegation.

## Context

With `-I`, an HTTP/2 response ends at the first DATA frame after the final head and the
transfer succeeds (BL-5391, BL-5468). curl 8.18.0 (nghttp2), measured with
`Record-CurlExchange.ps1 -Http2Origin prior-knowledge`:

| Request and answer | curl |
| --- | --- |
| `-I`, non-empty DATA, with or without `content-length: 3` | `* nghttp2 shuts down connection with error 1: PROTOCOL_ERROR`, `GOAWAY` error 1 with no debug data, `* shutting down connection #0`, exit 0; the next URL opens a new connection |
| `-I`, empty DATA ending the stream | exit 0, connection left intact, `GOAWAY` `shutdown` at the end |
| `-I -X GET`, DATA within `content-length` | exit 0, connection left intact, `GOAWAY` `shutdown` at the end |
| `-I -X GET`, DATA past `content-length` | as the first row |

## Decision

1. The rule is the length rule of RFC 9113 section 8.1.1 that ADR-0847 already applies, not
   "any DATA after a `-I` head": DATA past the length the head promises, which for HEAD is
   none, sends `GOAWAY` PROTOCOL_ERROR. On a body that is not wanted the transfer still
   succeeds, and the nghttp2 line is reported as a `-v` line.
2. `Http2Session.AcceptsNewStreams` is false once this client has sent a `GOAWAY`, as it
   already was once the peer had, so the connection is not reused or pooled.

## Consequences

An HTTP/2 connection that this client shut down for any protocol error takes no new stream.
