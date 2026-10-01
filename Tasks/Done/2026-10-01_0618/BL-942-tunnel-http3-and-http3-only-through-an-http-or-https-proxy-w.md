---
id: BL-942
title: Tunnel --http3 and --http3-only through an HTTP or HTTPS proxy with CONNECT-UDP as curl 8.21.0 does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-837]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Documentation/Planning/Decisions/ADR-0223-http-3-through-a-proxy-is-refused-as-curl-se-s-ngtcp2-build-refuses-it.md]
requirement: none
created: 2026-09-29
completed: 2026-10-01
---
# BL-942 — Tunnel --http3 and --http3-only through an HTTP or HTTPS proxy with CONNECT-UDP as curl 8.21.0 does

## Goal

An `https://` transfer with `--http3` or `--http3-only` and `-x http://…` or `-x https://…` opens a CONNECT-UDP tunnel (RFC 9298) over the proxy connection and runs QUIC inside it through the capsule protocol (RFC 9297), as curl 8.21.0 does, instead of refusing HTTP/3 with `HTTP/3 is not supported over an HTTP proxy` as the 8.18.0 build does (BL-837, ADR-0223).

## Context

- BL-837 measured curl.se's 8.18.0 build and pinned its behaviour for HTTP proxies: `--http3-only` fails with exit 3 before connecting, `--http3` connects over TCP with the refusal as its error text. ADR-0187 makes 8.21.0 the HTTP/3 reference, and 8.21.0 no longer refuses an HTTP proxy: `Curl_conn_may_http3` (`lib/vquic/vquic.c`, tag `curl-8_21_0`) only refuses SOCKS.
- In 8.21.0, `lib/cf-setup.c` `cf_setup_add_origin_filters` puts a capsule filter (`Curl_cf_capsule_insert_after`) and a QUIC filter on top of the HTTP proxy tunnel filter when the transport is QUIC; `lib/http_proxy.c` then opens the tunnel as `CONNECT-UDP` (`udp_tunnel`), over HTTP/1.1 through `Curl_cf_h1_proxy_insert_after(..., udp_tunnel)` (or HTTP/2 when the HTTPS proxy negotiates h2). `USE_PROXY_HTTP3` (an HTTP/3 proxy, `--proxy-http3`-like) is off by default and out of scope.
- Start by measuring: no 8.21.0 ngtcp2 build was installed on 2026-09-29 (only curl.se's 8.18.0 under WinGet). Install curl.se's current Windows build (8.22.0 or later has the same code path) and record the CONNECT-UDP request bytes with `Record-CurlExchange.ps1` as the proxy (extend it to answer the upgrade if needed; add it to `touches`).
- The TCP race for `--http3` goes through the proxy with an ordinary `CONNECT` as today.

## Acceptance criteria

- [x] The measured CONNECT-UDP request (bytes, curl version line) and curl's stderr for a refused CONNECT-UDP (e.g. the proxy answering 403) are copied into Notes for `--http3` and `--http3-only`.
- [x] Tests in `Curl.Protocol.Http.UnitTests` pin: `--http3-only` through an HTTP proxy sends the measured CONNECT-UDP request and runs HTTP/3 over the tunnel; a refused tunnel fails with the measured exit code and message; `--http3` races it against a TCP `CONNECT` as measured.
- [x] `HttpProtocolHandlerTests.Http3Proxy.cs`'s HTTP proxy cases are changed to the 8.21.0 behaviour, and ADR-0223's interim section is marked superseded.
- [x] `curl --ai-help` needs no change, or is changed and says so.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each touched library.

## Notes

### Measured 2026-10-01 (ADR-0289)

curl.se's Windows build, downloaded to a temp folder (WinGet's 8.18.0 left alone):
`curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 LibreSSL/4.3.2 ... nghttp2/1.70.0 ngtcp2/1.25.0 nghttp3/1.18.0 WinLDAP`,
Release-Date 2026-09-02, features include `HTTP3 HTTPS-proxy proxy-HTTP3`. `Record-CurlExchange.ps1`
was the proxy as it is; it needed no extension.

`--http3-only -x http://127.0.0.1:18942 https://example.com/ -sS`, proxy answering
`HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n`: exit 7, stderr
`curl: (7) CONNECT-UDP tunnel failed, response 403`, request bytes:

```
GET http://127.0.0.1:18942/.well-known/masque/udp/example.com/443/ HTTP/1.1\r\n
Host: 127.0.0.1:18942\r\n
User-Agent: curl/8.22.0\r\n
Proxy-Connection: Keep-Alive\r\n
Connection: Upgrade\r\n
Upgrade: connect-udp\r\n
Capsule-Protocol: ?1\r\n
\r\n
```

`--http3` with the same proxy and two connections: the same CONNECT-UDP request on the first,
then `CONNECT example.com:443 HTTP/1.1\r\nHost: example.com:443\r\nUser-Agent: curl/8.22.0\r\nProxy-Connection: Keep-Alive\r\n\r\n`
on the second; both answered 403; exit 7, stderr `curl: (7) CONNECT tunnel failed, response 403`
(the TCP attempt's message, not the QUIC one's). `-v` shows `* CONNECT-UDP: no ALPN negotiated`,
`* Establishing HTTP proxy UDP tunnel to example.com:443`, the request, the reply, then
`* CONNECT-UDP tunnel failed, response 403`. With the proxy delaying its reply by 1 s
(`--trace-time`), the TCP CONNECT started 103 ms after the CONNECT-UDP, before either reply.

Answered `101 Switching Protocols` (or `200 OK`), `-v` shows `* CONNECT-UDP phase completed for
HTTP proxy`, `* CONNECT-UDP tunnel established, response 101` (or `200`), then
`* SSL Trust Anchors:` with no `Trying` line for QUIC; the bytes after the head are
`00 44 b1 00 c5 00 00 00 01 ...`: a `DATAGRAM` capsule (type 0, length 0x4b1 = 1201, context ID 0)
holding a 1200-byte QUIC Initial.

`--http3-only --proxy1.0 127.0.0.1:18942 https://[::1]:8443/ -U u:p --proxy-header "X-P: q"`:
`GET http://127.0.0.1:18942/.well-known/masque/udp/%3A%3A1/8443/ HTTP/1.0`, then `Host`,
`Proxy-Authorization: Basic dTpw`, `User-Agent`, `Proxy-Connection`, `Connection`, `Upgrade`,
`Capsule-Protocol`, `X-P: q`. Through `-x https://localhost:18942 --proxy-insecure`:
`GET https://localhost:18942/.well-known/masque/udp/example.com/443/ HTTP/1.1`, `Host: localhost:18942`.
`--http3-only -x socks5://...` is still exit 3 `HTTP/3 is not supported over a SOCKS proxy`.

### Decisions (ADR-0289)

- The Http handler refuses only SOCKS now; the connector owns the tunnel, so the request bytes
  are pinned in `Curl.Networking.UnitTests` (`HttpProxyTunnelTests`, `TcpConnectorQuicTests.UdpTunnel`,
  which runs a real QUIC handshake through capsules), and the Http tests pin that QUIC is asked
  for through the proxy, the exit 7 refusal, and the race's outcome.
- When both race legs fail through a proxy, the TCP CONNECT's result is the transfer's (measured).
- Not done, left as the simplest behaviour until measured: answering a `407` to CONNECT-UDP,
  `--preproxy` under the UDP tunnel, `-v`'s `>`/`<` tunnel header lines (Curl prints none for
  CONNECT either).
- `touches` gained ADR-0223's file to mark its interim section superseded (acceptance criterion);
  no task in Doing on `origin/work/dark-factory` named it.
- `HttpExchangeLog.DowngradeOf` (BL-922) was at complexity 14 in a touched library; its message
  choice moved into `DowngradeMessage` so the library has no failing member.
- `--ai-help` has no text about HTTP/3 through a proxy, so it needs no change.
- `Measure-CodeQuality.ps1`: `Curl.Networking.UnitLibrary` and `Curl.Protocol.Http.UnitLibrary`
  100% line, 100% branch, 0 failing members.

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. --http3 and --http3-only through an HTTP or HTTPS proxy run QUIC in a CONNECT-UDP tunnel as curl 8.22.0 does
