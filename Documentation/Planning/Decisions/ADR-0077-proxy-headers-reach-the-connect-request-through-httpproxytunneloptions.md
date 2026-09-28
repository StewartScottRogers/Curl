# ADR-0077 — `--proxy-header` values reach the CONNECT request through `HttpProxyTunnelOptions`

- **Status:** Accepted
- **Date:** 2026-09-27
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

BL-296 sent `HttpRequestOptions.ProxyHeaders` on a forward-proxy request head, but the
CONNECT request that opens a tunnel is written by `Curl.Networking`'s `HttpProxyTunnel`
from `HttpProxyTunnelOptions`, which carried only the `User-Agent` and the credential
encoding. BL-347 had to choose where the proxy headers travel to it: on
`HttpProxyTunnelOptions`, or on `ProxyEndpoint` / `ConnectTarget` in
`Curl.Protocol.Abstractions`.

Measured against curl 8.21.0 (mingw, Schannel, the Windows reference, ADR-0018) with
`Record-CurlExchange.ps1` on 2026-09-27 (the cases are in BL-347's Notes): curl's own
CONNECT headers are `Host`, `Proxy-Authorization`, `User-Agent` and
`Proxy-Connection: Keep-Alive`, in that order; a `--proxy-header` value naming one of them
(case-insensitively, with a colon or semicolon after the name) removes it; and the proxy
header lines follow in command-line order, rebuilt as `Name: value`. `-H` values never
reach CONNECT, and a `-H` value naming one of curl's CONNECT headers changes nothing.

## Decision

1. `HttpProxyTunnelOptions` gains `ProxyHeaders`, an init-only list defaulting to empty.
   `CurlComposition.CreateProxyTunnelOptions` copies `CommandLineOptions.ProxyHeaders` into
   it; `-H` values are never copied.
2. `HttpProxyTunnel.BuildConnectRequest` reads each value with `HttpProxyTunnelHeader`,
   which follows the CONNECT rules curl 8.21.0 was measured with (libcurl builds this
   request with its dynamic-headers list, not the function that writes `-H` values, so the
   rules differ from `HttpCustomHeader`'s): the name ends at the first colon or semicolon;
   `Name: value` sends `Name: ` and the value without its leading spaces and tabs;
   `Name:` with no value sends nothing; `Name;` sends `Name: `; anything after that
   semicolon sends nothing; no separator or an empty name sends nothing and overrides
   nothing.

## Consequences

- No change to `Curl.Protocol.Abstractions`, so no protocol handler is touched and the
  tunnel stays inside `TcpConnector`, off the protocol handlers' seam.
- The proxy headers are fixed per run, like the `User-Agent` already on the same options:
  every URL on the command line shares them, as `--proxy-header` is global in curl. A
  future per-URL `--next` group with its own `--proxy-header` would need them to move onto
  the connect target.
- `Curl.Networking` holds a second header reader beside `Curl.Protocol.Http`'s
  `HttpCustomHeader`; they differ on purpose, as the measured bytes do (`X-E;` sends
  `X-E: ` on CONNECT and `X-E:` on a request).

## Alternatives considered

- Carrying the headers on `ProxyEndpoint` or `ConnectTarget`: per-URL, but a change to the
  shared contract that every protocol task depends on, for a value that is per run today.
- Reusing `HttpCustomHeader`: it lives in a protocol library `Curl.Networking` must not
  reference, and its rules produce different bytes from curl's CONNECT request.
