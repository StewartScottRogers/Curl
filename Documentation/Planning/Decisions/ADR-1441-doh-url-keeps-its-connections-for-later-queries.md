# ADR-1441 — `--doh-url` keeps its connections for later queries

- **Status:** Accepted
- **Date:** 2026-11-26
- **Amends:** ADR-1435, which closed each connection once one name's queries were answered

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-6051.

## Context

Measured 2026-11-26 with `-v --trace-config doh` over two and three URLs against nginx 1.30.8 serving
DoH over HTTP/2 and over HTTP/1.1, with curl 8.18.0 on Ubuntu (OpenSSL, nghttp2) and curl 8.18.0 on
Windows (curl.se's LibreSSL, nghttp2 build). Both behaved alike:

- HTTP/2: a later name goes on the first connection, `Reusing existing https: connection with host
  H` and stream 5, then `Multiplexed connection found`, `Reusing ...` and stream 7; `Connection #1
  ... left intact` once.
- HTTP/1.1: the first name's second query waits and then reuses `Connection #1`. For a later name the
  first query reuses `#1` and the second, finding it busy, opens a new connection at once (`#3`, after
  the second URL's `#2`). A third name's queries reuse `#1` and `#3`.
- Windows' own `curl.exe` 8.23.0 (Schannel, no HTTP/2) opens two connections at once and reuses both
  for the next name.

The HTTPS-record query was not measured: no curl on the machine lists `HTTPSRR` or `ECH`.

## Decision

- `DohDnsResolver` keeps every connection its response leaves open: an HTTP/2 one whose streams were
  all answered, an HTTP/1.x one with a `Content-Length` or chunked body and no `Connection: close`
  (HTTP/1.0 only with `keep-alive`). `DisposeAsync` closes them.
- A later query takes the kept HTTP/2 connection, else the lowest-numbered kept HTTP/1.x one, else
  opens one. Two address queries share a kept HTTP/2 connection with the lines above.
- Over TLS, an HTTP/1.x server's first name has its second query after the first one's answer, on the
  same connection when it stays open. The order stays ADR-0380's: traced AAAA first, untraced A first.
- A plain `http://` DoH URL still opens two connections at once for its first name, then reuses them.
- A connection serves one lookup at a time. A lookup running beside another opens its own, where curl
  would multiplex both on one HTTP/2 connection.
- A kept connection that answers nothing was closed in the meantime: the query is asked again on a new
  one. curl's lines for finding it dead were not measured, so none is written.
- The HTTPS-record query follows the same rules: it goes first (ADR-0727) and the address queries
  reuse its connection.
- A connection's `-v` number is the `%{conn_id}` it takes when that is past the next DoH number, so a
  caller that numbers the transfers first gets curl's `#3`. The console numbers a transfer before its
  DoH connections (ADR-1455).

## Consequences

The composition roots do not dispose the resolver yet, so a kept connection stays open until the
process ends; a follow-up task disposes it.
