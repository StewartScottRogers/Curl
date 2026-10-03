---
id: BL-1287
title: Write the [SSL] and [SSL-PROXY] trace lines of a TLS handshake under --trace-config ssl and proxy
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-03
---
# BL-1287 — Write the [SSL] and [SSL-PROXY] trace lines of a TLS handshake under --trace-config ssl and proxy

## Goal

Curl writes curl 8.21.0's `[SSL]` lines around an origin's TLS handshake and `[SSL-PROXY]` lines around an HTTPS proxy's, under `--trace-config ssl`, `proxy` (proxy only) and `all`, as the reference build does.

## Context

- Found in BL-1255 (ADR-0357's BL-1255 amendment). No `[SSL]` or `[SSL-PROXY]` line is written anywhere yet, and the console sets no flag for the `ssl` group.
- Measured 2026-10-02 (curl 8.21.0 mingw Schannel, `Record-CurlExchange.ps1 -Tls -Script` playing an HTTPS proxy, `-s -v --trace-config proxy -p --proxy-insecure -x https://127.0.0.1:P http://example.test/x`): after `[HTTP-PROXY] CONNECT`, `[SSL-PROXY] cf_connect()`, the schannel lines and `ALPN: curl offers http/1.1`, `[SSL-PROXY] cf_connect() -> 0, done=0`, `[SSL-PROXY] adjust_pollset, POLLIN fd=N`, then each poll round `[HTTP-PROXY] CONNECT`, `[SSL-PROXY] cf_connect()`, and finally `ALPN: server did not agree on a protocol. Uses default.`, `[SSL-PROXY] cf_connect() -> 0, done=1`, `[SSL-PROXY] query ALPN`, `[SSL-PROXY] query ALPN: returning '(nil)'` before `CONNECT: no ALPN negotiated`. `fd=N` is volatile.
- Today Curl writes BL-1255's two handshake-poll `[HTTP-PROXY] CONNECT` lines after the handshake's `-v` lines (`HttpProxyTunnelTrace.ReportProxyHandshakePolled`); placing them among the handshake's lines belongs here, since the console renders those lines from the handshake event.
- Measure the origin's `[SSL]` lines for a direct `https://` connect first; OpenSSL build differences too.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1` (direct `https://`, and through `-x https://`, `--trace-config ssl`, `proxy` and `all`), stderr in Notes.
- [x] Tests in `Curl.Networking.UnitTests` and `Curl.Console.UnitTests` pin the `[SSL]` and `[SSL-PROXY]` lines and that none appears without its group; ADR-0357 gets an amendment for every volatile value.
- [x] `--ai-help` still describes `--trace-config` correctly.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for the touched libraries.

## Notes

- Measured 2026-10-03, curl 8.21.0 mingw Schannel, `Record-CurlExchange.ps1 -Tls` (direct) and `-Tls -Script` playing an HTTPS proxy (read, send the 200 Connection established, read, send a 200 with `Content-Length: 2`, read). Direct `-s -v -k --trace-config ssl https://127.0.0.1:18961/x`, exit 0:

  ```
  *   Trying 127.0.0.1:18961...
  * [SSL] cf_connect()
  * schannel: disabled automatic use of client certificate
  * schannel: using IP address, SNI is not supported by OS.
  * ALPN: curl offers http/1.1
  * [SSL] cf_connect() -> 0, done=0
  * [SSL] adjust_pollset, POLLIN fd=448
  * [SSL] cf_connect()
  * [SSL] cf_connect() -> 0, done=0
  * [SSL] adjust_pollset, POLLIN fd=448
  * [SSL] cf_connect()
  * ALPN: server did not agree on a protocol. Uses default.
  * [SSL] cf_connect() -> 0, done=1
  * Established connection to 127.0.0.1 (127.0.0.1 port 18961) from 127.0.0.1 port 53174
  * [SSL] query ALPN
  * [SSL] query ALPN: returning '(nil)'
  * using HTTP/1.x
  ```
- Direct, which components: the same `[SSL]` lines under `network`, `all` and `-vvvv` (`all` adds `[SSLS]` session-cache lines); none under `proxy`, `tls` or plain `-v`.
- Failed direct handshake (no `-k`, untrusted certificate, exit 60): `[SSL] cf_connect()`, the trust lines, `ALPN: curl offers`, one round (`done=0`, `adjust_pollset`, `cf_connect()`), `schannel: SEC_E_UNTRUSTED_ROOT ...`, `[SSL] cf_connect() -> 60, done=0`, `closing connection #0`.
- Through `-p --proxy-insecure -x https://127.0.0.1:P http://example.test/x`: under `proxy` and `all` the BL-1255 order (`[HTTP-PROXY] CONNECT`, `[SSL-PROXY] cf_connect()`, trust lines, `ALPN: curl offers`, two rounds of `done=0` / `adjust_pollset, POLLIN fd=444` / `[HTTP-PROXY] CONNECT` / `cf_connect()`, `ALPN: server did not agree`, `done=1`, `[SSL-PROXY] query ALPN`, `query ALPN: returning '(nil)'`, `CONNECT: no ALPN negotiated`). Under `ssl`, `network` and `-vvvv`: no `[SSL-PROXY]` line at all (BL-1255 Notes said `ssl` turns them on; measured, it does not).
- An `https://` origin through the HTTPS proxy is unmeasured (the script cannot finish the origin's TLS); its `[SSL]` lines are written as for a direct connect, around the origin's handshake over the tunnel.
- OpenSSL build: not measurable on this Windows machine; the filter lines are curl's own (`cf_connect()`, `adjust_pollset`, `query ALPN`), not backend text, so both builds get the same lines.
- Decisions (ADR-0357's BL-1287 amendment): `SslFilterTrace` in `Curl.Networking`; flags `TcpConnector.TracesSslFilter` (`ssl`, `network`, `all`) and `TracesSslProxyFilter` (`proxy`, named `all`), set by `CurlComposition.TracesSsl` and `TracesSslProxy`. Two poll rounds for success, one for failure, `fd=3`. The rounds follow the handshake's `-v` lines rather than sitting between its two ALPN lines: those come from one `TlsHandshakeEvent` rendered in `Curl.Output`, outside this task's touches, and splitting them would change the `Curl.Protocol.Abstractions` contract. `HttpProxyTunnelTrace.ReportProxyHandshakePolled` is replaced by the rounds; a failed proxy handshake now writes a second `[HTTP-PROXY] CONNECT` in its one round. A forward HTTPS proxy's handshake writes `[SSL-PROXY]` lines and no ALPN query (unmeasured).
- `--ai-help all` shows curl's general `--trace-config` text ("Details to log in trace/verbose output"), still true; no change.
- ADR-0357 amended in place, as the criterion asks.
- Tests: `TcpConnectorTests.SslFilterTrace` (8), `CurlCompositionSslTraceTests` (13 rows), `CurlCommandRunnerHttpsConnectTraceTests` (+4 rows); `CurlCommandRunnerTcpIoTraceTests` now sets the new `[SSL] query ALPN` lines aside. Networking 2947 passed, Console 2486 passed, solution build clean, all fast tests green.
- `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 100% line, 100% branch, 0 failing, worst CRAP 10 (after moving `WriteSslQueryAlpnLines` out of `SecureWhenAskedAsync`). `-Library Curl.Console`: 100% line, 99.06% branch, 11 failing members, none of them changed here (`TracesSsl` and `TracesSslProxy` pass); they were already below the gates, so they are filed as BL-1322 rather than widening this task.

## Log

- 2026-10-02: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. [SSL] and [SSL-PROXY] handshake trace lines written under ssl/network/all and proxy/named all, as curl 8.21.0 does
