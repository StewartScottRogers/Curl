# ADR-0250 — HTTP/3 through an HTTP proxy runs QUIC in a CONNECT-UDP tunnel

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-942.
Supersedes decision 4 of ADR-0223 (the interim refusal of HTTP/3 through an HTTP or HTTPS
proxy), under ADR-0187, which makes curl 8.21.0 the HTTP/3 reference.

## Context

curl 8.21.0 no longer refuses HTTP/3 through an HTTP proxy: `Curl_conn_may_http3` refuses
only SOCKS, and `cf_setup_add_origin_filters` puts a capsule filter and the QUIC filter on
top of the HTTP proxy tunnel, which `lib/http_proxy.c` opens as CONNECT-UDP (RFC 9298) with
QUIC packets carried as HTTP Datagram capsules (RFC 9297). No 8.21.0 build with ngtcp2 was
installed, so curl.se's 8.22.0 Windows build (LibreSSL, ngtcp2 1.25.0), which has the same
code path, was measured against `Record-CurlExchange.ps1` as the proxy on 2026-10-01. The
requests, replies and `-v` lines are in BL-942's Notes.

## Decision

1. `--http3` and `--http3-only` with an `https://` URL through an HTTP, HTTP/1.0 or HTTPS
   proxy ask the connector for QUIC with the proxy on the target. A SOCKS proxy is still
   refused as ADR-0223 decides (re-measured on 8.22.0: exit 3, unchanged text).
2. `TcpConnector.ConnectMultiplexedAsync` resolves and dials the proxy (not the target),
   runs the proxy's TLS handshake for an HTTPS proxy (ALPN `http/1.1`, then
   `CONNECT-UDP: 'h' negotiated` or `CONNECT-UDP: no ALPN negotiated`), and sends the
   measured request: `GET <scheme>://<proxy>/.well-known/masque/udp/<host>/<port>/` in
   absolute form, HTTP/1.0 for an HTTP/1.0 proxy, then `Host` (the proxy),
   `Proxy-Authorization`, `User-Agent`, `Proxy-Connection: Keep-Alive`,
   `Connection: Upgrade`, `Upgrade: connect-udp`, `Capsule-Protocol: ?1` and the
   `--proxy-header` lines. The host is percent-encoded as RFC 6570 does (`::1` is `%3A%3A1`).
3. A `101` or any `2xx` opens the tunnel (`CONNECT-UDP tunnel established, response <n>`);
   any other status is exit 7 `CONNECT-UDP tunnel failed, response <n>`, and a reply curl
   gives up on is exit 56 with the CONNECT path's message.
4. Inside the tunnel `CapsuleDatagramChannel` carries each QUIC datagram as one `DATAGRAM`
   capsule (type 0, length, context ID 0, payload), and `QuicDialer.DialThroughTunnelAsync`
   runs the same handshake as a direct dial with no `Trying` line. The QUIC connection
   reports the proxy's address as its remote end point. A tunnel the proxy closes fails the
   channel's receive as a reset socket, so the handshake fails with exit 56 `QUIC: recvfrom()`.
5. `--http3` races the tunnel against an ordinary TCP `CONNECT` exactly as it races QUIC
   against TCP without a proxy (measured: the CONNECT starts once the CONNECT-UDP fails, or
   100 ms in while the proxy is silent). When both fail, the transfer fails with the TCP
   `CONNECT`'s exit code and message, as 8.22.0 printed `CONNECT tunnel failed, response 403`.
6. Left as they are, the simplest drop-in behaviour until measured otherwise: the
   CONNECT-UDP request sends only the pre-emptive `Proxy-Authorization` (a `407` is the
   tunnel's exit 7, with no challenge answered); `--preproxy` is not used for the tunnel;
   an HTTPS proxy that negotiates `h2` still gets HTTP/1.1, since the proxy handshake offers
   only `http/1.1`, as for CONNECT (ADR-0190).

## Consequences

- `HttpProtocolHandler.Http3ProxyRefusalOf` refuses SOCKS proxies only; `TriesQuic` lets an
  HTTP proxy through; `HttpTransferMessages.Http3NotOverHttpProxy` is gone.
- `HttpProxyTunnel.BuildConnectUdpRequest`, `HttpProxyTunnelReply.OpensUdpTunnel`,
  `CapsuleDatagramChannel` and `TcpConnector.UdpTunnel.cs` implement the tunnel;
  `TcpConnectorQuicTests.UdpTunnel.cs` runs a real QUIC handshake through it.
- Curl does not print the tunnel request's `>` and `<` header lines with `-v`, as it prints
  none for a CONNECT today.
