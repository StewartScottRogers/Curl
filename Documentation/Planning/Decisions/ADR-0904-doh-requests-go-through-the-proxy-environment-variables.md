# ADR-0904: DoH requests go through the proxy environment variables

- Status: Accepted
- Date: 2026-10-26
- Task: BL-3566 (CGR-037)
- Decided by Claude under Stewart's delegation

## Context

ADR-0152 gave the DoH connector no proxy. curl 8.22.0's DoH sub-transfers are easy handles of
their own that `lib/doh.c` gives no `CURLOPT_PROXY` and no `CURLOPT_NOPROXY`, so libcurl's
`detect_proxy` reads the proxy environment variables for the DoH URL. Measured on the Schannel
build with `Record-CurlExchange.ps1` as a proxy answering 403:

- `http_proxy=http://127.0.0.1:21803 no_proxy=foo.test`, `--doh-url http://127.0.0.1:21800/dns-query`:
  the proxy gets two `POST http://127.0.0.1:21800/dns-query HTTP/1.1` requests with `Host`,
  `Accept: */*`, `Proxy-Connection: Keep-Alive`, `Content-Type` and `Content-Length`; exit 6.
- `https_proxy=http://127.0.0.1:21803`, `--doh-url https://127.0.0.1:21801/dns-query`: two
  `CONNECT 127.0.0.1:21801 HTTP/1.1` with `Host` and `Proxy-Connection: Keep-Alive` only, no
  `User-Agent`; exit 6.

## Decision

`EngineComposition.CreateDohResolver` chooses the DoH requests' proxy with `ProxySelector` over the
environment for the DoH URL (`DohProxyOf`), never `-x` or `--noproxy`, and sets
`DohDnsResolver.Proxy`. An `http://` DoH URL through an HTTP or HTTPS proxy is POSTed to the proxy
(`IsForwardProxy`) in absolute form with `Proxy-Connection: Keep-Alive` after `Accept`; an
`https://` one, or one through a SOCKS proxy, is tunnelled by the DoH `TcpConnector`, whose
`HttpProxyTunnelOptions` carry no `User-Agent`. Unusable proxy text leaves every name unresolved
(exit 6), as a failed DoH probe does.

## Consequences

Both measured repros give real curl's exit code, stdout, stderr and request bytes. The DoH
`-v` trace lines through a proxy are not yet pinned against curl.
