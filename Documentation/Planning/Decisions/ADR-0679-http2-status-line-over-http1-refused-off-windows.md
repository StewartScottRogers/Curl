# ADR-0679 — An `HTTP/2` or `HTTP/3` status line over HTTP/1.x is refused off Windows

- **Status:** Accepted; supersedes ADR-0517; amended by ADR-0788 (Windows too)
- **Date:** 2026-10-17

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2715
(gap finding GF-0083, upstream test1704).

## Context

ADR-0517 parsed an `HTTP/2 1xx` line over HTTP/1.x as an HTTP/2 interim head, so the
`HTTP/1.1 200` after it in upstream test1704 failed with exit 8, `Version mismatch (from HTTP/2
to HTTP/1)`. That was taken from reading curl's source, not measured. The exit code then matched,
but the gap office's Linux and macOS runs of 2026-10-17 still measured test1704 as a gap:
`stderr differs`.

Measured with Linux curl 8.18.0 (OpenSSL 3.5.5, nghttp2 1.68.0, from WSL Ubuntu) through
`Record-CurlExchange.ps1` (BL-2715 Notes): curl fails at the **first** line,
`curl: (8) Version mismatch (from HTTP/1 to HTTP/2)`, and never writes `< HTTP/2 101 OK`. The
same happens with or without `--http2`, for a final `HTTP/2 200`, and for `HTTP/2 200` after an
`HTTP/1.1 100`. An `HTTP/3 ddd` line fails with exit 1, `Unsupported HTTP version (3.0) in
response`. `HTTP/2 99`, `HTTP/2 abc`, `HTTP/2` and `HTTP/2.0 200` are still taken as an assumed
`HTTP/1.0 200`, exit 0.

## Decision

- Off Windows, `HttpResponseHeadReader.RefusesHttp2StatusLines` fails any status line over
  HTTP/1.x that `HttpStatusLine.NamesHttp2OrHttp3` (`HTTP/2 ` or `HTTP/3 ` and three digits),
  interim or final, before the line is reported or written: `HTTP/2` with exit 8 and
  `Version mismatch (from HTTP/1 to HTTP/2)`, `HTTP/3` with exit 1 and `Unsupported HTTP
  version (3.0) in response` (`HttpStatusLine.RefusalOverHttp1`).
- The heads of an HTTP/2 or HTTP/3 stream, and of stream 1 after an h2c upgrade's
  `HTTP/1.1 101`, are not over HTTP/1.x and are parsed as before.
- On Windows nothing changes: the Schannel build takes the line as an assumed `HTTP/1.0 200`.
- ADR-0517's check of a later head against the 1xx head's version is removed: no 1xx head over
  HTTP/1.x can now name a version other than HTTP/1.

## Consequences

- test1704 answers as curl's OpenSSL build does: exit 8, no stdout, curl's message on stderr.
- Measured on curl 8.18.0 rather than the targeted 8.22.0, the newest OpenSSL build at hand;
  test1704's own `errorcode` 8 in the 8.22.0 tarball agrees. The gap office's next Linux and
  macOS runs confirm it.
