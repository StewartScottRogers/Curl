# ADR-0788: An `HTTP/2` or `HTTP/3` status line over HTTP/1.x is refused on every platform

- **Status:** Accepted; amends ADR-0679
- **Date:** 2026-10-23

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-3270
(upstream test1704).

## Context

ADR-0679 refused an `HTTP/2 ddd` or `HTTP/3 ddd` status line over HTTP/1.x off Windows only,
because the Windows reference then was System32's Schannel build, which has no HTTP/2 and takes
the line as an assumed `HTTP/1.0 200`. ADR-0871 turned HTTP/2 on for Windows and BL-3268 ran the
upstream HTTP/2 cases there, so test1704 now runs on Windows and expected exit 8 where Curl exited 0.

Measured on Windows with an HTTP/2-built curl (curl 8.18.0, LibreSSL, nghttp2 1.68.0, from WinGet)
through `Record-CurlExchange.ps1`: test1704's `HTTP/2 101 OK` then `HTTP/1.1 200 OK`, with or
without `--http2`, fails with exit 8, `curl: (8) Version mismatch (from HTTP/1 to HTTP/2)`, and no
stdout - the same answer as the Linux OpenSSL build.

## Decision

- `HttpResponseHeadReader` refuses such a line on every platform; the `RefusesHttp2StatusLines`
  switch is removed, as no platform takes the line as an assumed `HTTP/1.0 200` any more.
- `HTTP/3 ddd` keeps ADR-0679's exit 1, `Unsupported HTTP version (3.0) in response`, the answer of a
  curl built without HTTP/3, which Curl is on every platform. (That WinGet build, which has HTTP/3,
  answers exit 8, `Version mismatch (from HTTP/1 to HTTP/3)`.)

## Consequences

- test1704 is held on the conformance ratchet on Windows as on Linux and macOS.
- A Windows script that read such a reply as a 200 now sees exit 8, as with an HTTP/2-built curl.
