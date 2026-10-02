# ADR-0333 — A chunked `407` to CONNECT is discarded and the connection reused

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-862.
Amends ADR-0186, which sent the answer to a chunked `407` on a new connection.

## Context

ADR-0186 made `TcpConnector` answer a CONNECT `407` on the same connection when the reply
leaves it open, but treated a `Transfer-Encoding: chunked` body as not reusable and dialled
the proxy again. curl 8.21.0 (mingw, Schannel) measured 2026-10-01 with
`Record-CurlExchange.ps1` as the proxy (BL-862 Notes) does not: it prints
`CONNECT responded chunked` and `Ignore chunked response-body`, reads the chunked body
through to its trailer, and sends the authenticated CONNECT on the same connection. A
malformed chunked body ends the transfer with exit 56 and the chunk parser's message, and a
body cut short with exit 56 `Proxy CONNECT aborted`; neither dials again.

## Decision

`HttpProxyTunnelReply.IsChunked` says the body is chunked, and `LeavesConnectionReusable`
depends only on `Connection: close` and `Proxy-Connection: close`. When the answer goes on
the same connection, `HttpProxyTunnel.DiscardChunkedBodyAsync` reads the body one byte at a
time through `HttpProxyTunnelChunkedBody`, a copy of the states of curl's `http_chunks.c`:
up to 16 hex digits of size, anything up to the LF skipped (chunk extensions), the data, CRs
then an LF after it, trailer lines and the empty line. Its failures carry curl's measured
messages (`chunk hex-length char not a hex digit: 0x7a`, `chunk hex-length longer than 16`,
`invalid chunk size: '<hex>'`, and `Failure when receiving data from the peer` for a missing
LF); `TcpConnector` returns them as `CurlExitCode.RecvError` (56).

## Consequences

- One connection, two CONNECTs, as curl, after a chunked `407` that stays open.
- The `-v` lines `CONNECT responded chunked`, `Ignore chunked response-body` and
  `chunk reading DONE` are not written; the CONNECT retry's other `-v` lines are BL-863's.
- curl's 4096-byte limit on one trailer line is not copied: a longer trailer is read through.

## Alternatives considered

- Keep dialling again: costs a connection curl does not make, and turns curl's exit 56 on a
  malformed body into a working retry.
- Read the chunk data in blocks: faster for a large body, but a `407` body is small and
  reading one byte at a time keeps the next reply on the connection with no buffering.
