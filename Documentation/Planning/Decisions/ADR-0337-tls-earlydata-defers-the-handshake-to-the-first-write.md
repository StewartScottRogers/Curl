# ADR-0337 — `--tls-earlydata` defers the handshake to the first write and sends it as 0-RTT early data

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1105.
It builds on ADR-0319 (`--ssl-sessions`, the run's `TlsSessionCache`) and ADR-0140 (the
hand-built TLS provider).

## Context

curl 8.21.0's OpenSSL build sends the first request as TLS 1.3 0-RTT early data when
`--tls-earlydata` is given and the session it resumes allows early data. Read from the source
at tag `curl-8_21_0` (no OpenSSL-build curl was available to `Record-CurlExchange.ps1` on the
lane that did this work):

- `lib/vtls/vtls.c` `Curl_on_session_reuse`: early data is used only when the session's
  `max_early_data` is above zero and its ALPN protocol is one the connection offers; the hello
  then offers that protocol alone, and the connection reports it as negotiated before the
  handshake. `-v`: `SSL session allows N bytes of early data, reusing ALPN 'P'`.
- `ssl_cf_connect_deferred`: the connect returns at once; the handshake runs on the first send.
- `lib/vtls/openssl.c` `ossl_send_earlydata`: `SSL sending N bytes of early data`, then the
  handshake's lines, then `Server accepted N bytes of TLS early data.` or
  `Server rejected TLS early data.`; rejected or unsent bytes go out after the handshake.

`SslStream` has no early data, and the HTTP handler writes its request after the connect.

## Decision

1. **`--tls-earlydata` routes to the hand-built client** (`TlsClientRouting`, beside the
   `--no-sessionid` and `--ssl-allow-beast` rows), on every platform, so both builds behave as
   the OpenSSL build does.
2. **The connect is deferred, not the handler changed.** When the option is on, TLS 1.3 is in
   range and the session taken from the cache allows early data with an offered ALPN protocol,
   `HandBuiltTlsProvider` returns an `EarlyDataTlsConnection` at once, reporting the session's
   ALPN protocol. Its first write runs the handshake with those bytes as early data
   (`TlsClientConnection.ConnectWithEarlyDataAsync`, `Tls13ClientConnection.ConnectWithEarlyDataAsync`),
   at most the session's `max_early_data_size` of them; whatever the server does not accept,
   all of it when it rejects early data or picks TLS 1.2, is written once the handshake is done.
   A read or flush before any write runs the handshake with no early data.
3. **A failed deferred handshake keeps the connect's exit code.** The first write throws
   `DeferredTlsHandshakeFailedException` (an `IOException` in `Curl.Protocol.Abstractions`)
   carrying the exit code and message the connect would have failed with, and
   `HttpConnectionSend` reports those instead of exit 55.
4. **Otherwise nothing changes:** no session, a session allowing no early data, one without an
   ALPN protocol or with one not offered, or a TLS 1.2 ceiling run the handshake at connect.

## Consequences

- `--write-out`'s `%{tls_earlydata}` still prints 0; reporting the bytes sent is a follow-up.
- The `-v` lines are pinned from the source reading above; a measurement against an OpenSSL
  build should confirm them when one is available.

## Alternatives considered

- **Hand the request to the TLS provider before connecting.** Every protocol handler would have
  to change its order of operations; deferring inside the connection keeps the handlers as they are.
- **Leave `--tls-earlydata` on `SslStream` and ignore it.** Not a drop-in replacement for the
  OpenSSL build, and the hand-built client already sends early data (BL-701).
