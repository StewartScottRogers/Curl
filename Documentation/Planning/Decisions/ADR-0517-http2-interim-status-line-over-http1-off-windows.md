# ADR-0517: An HTTP/2 or HTTP/3 interim status line over HTTP/1.x is parsed as that version's 1xx off Windows

- Status: Accepted
- Date: 2026-10-15
- Task: BL-2326 (gap finding GF-0083, upstream test1704)
- Decided by Claude under Stewart's delegation.

## Context

Upstream test1704 answers an h2c upgrade request with `HTTP/2 101 OK`, then `HTTP/1.1 200 OK`.
curl 8.22.0's OpenSSL build on Linux and macOS, built with HTTP/2, parses `HTTP/2 101` as an
HTTP/2 interim response, records HTTP/2 as the version seen, and fails the following
`HTTP/1.1 200` with exit 8, `Version mismatch (from HTTP/2 to HTTP/1)`. The Windows Schannel
reference has no h2c and takes the line as an assumed `HTTP/1.0 200`, so the gap analysis
excludes the case there. Curl took the line as an assumed `HTTP/1.0 200` everywhere and exited 0.

## Decision

- `HttpResponseHeadReader.ParsesHttp2InterimStatusLines`, true everywhere but Windows, makes a
  status line `HTTP/2 1xx` or `HTTP/3 1xx` (three digits, `HttpStatusLine.IsHttp2OrHttp3Interim`)
  parse as that version's 1xx over HTTP/1.x. On Windows the line stays an assumed `HTTP/1.0 200`.
- Each status line after a 1xx head must name the same major version as that head, or the read
  fails with exit 8 and curl's `Version mismatch` message (`Http1VersionSeen.ThrowIfMismatched`).
  The upgraded stream's HTTP/2 head after an h2c upgrade's `HTTP/1.1 101` is exempt, as it is not
  a version change on the HTTP/1.x connection.
- Only interim lines change. A final `HTTP/2 200` over HTTP/1.x is still an assumed
  `HTTP/1.0 200` on every platform: no upstream case measured it otherwise, and the narrowest
  change that matches test1704 keeps every other measured case where it is.

## Consequences

test1704 exits 8 with no stdout on Linux and macOS, matching the platform's curl; Windows is
unchanged. The platform split is injected as an init property, so both answers are unit tested
on any platform.
