# ADR-1424 — `--doh-url` offers `h2` by ALPN and speaks HTTP/2 to a server that picks it

- **Status:** Accepted
- **Date:** 2026-11-26
- **Amends:** ADR-0152, whose DoH handshake offered `http/1.1` alone

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-6015.

## Context

Fitness finding STD-221: a DoH server that speaks only HTTP/2 (Unbound, dnsdist) could not
be used, so every name failed with exit 6, where curl 8.18.0 on Ubuntu resolves through it.
curl's `lib/doh.c`, built with nghttp2, asks each DoH sub-transfer for
`CURL_HTTP_VERSION_2TLS`: HTTP/2 over TLS, HTTP/1.1 for a plain `http://` URL. The
`--http1.1` family of options is not copied to the sub-transfers. ADR-1274 already has Curl
offer `h2` by default on every platform.

## Decision

- An `https://` DoH URL's handshake offers `h2,http/1.1` on every platform, whatever the
  transfer's HTTP version options say. The Windows reference builds without nghttp2 offer
  `http/1.1` alone; Curl follows its own ADR-1274 line instead, so one binary never speaks
  HTTP/2 to origins and refuses it to DoH servers.
- A connection that picked `h2` carries the POST on stream 1 of a `Curl.Http2` connection,
  with the header fields the HTTP/1.1 head carries. A failed connection or stream, no final
  status, or a body over curl's 3000 bytes fails the query as the HTTP/1.1 path does.
- The A and AAAA queries keep a connection each, as before. curl sets
  `CURLOPT_PIPEWAIT` and can multiplex both on one HTTP/2 connection; that changes only the
  connection count and the `-v` lines, and is left to a task of its own.

## Consequences

The finding's repro against Unbound 1.26.2 with the Linux build now prints
`Trying 192.0.2.20:1...` and exits 28, as curl does. The HTTP/2 sub-transfer's `-v` lines
follow the shape of the HTTP handler's HTTP/2 lines; they have not been compared byte for
byte with curl's.
