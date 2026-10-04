# ADR-0408 — `--proxy-http2` tunnels through an HTTP/2 HTTPS proxy off Windows, and `--proxy-http3` stays refused

- **Status:** Accepted
- **Date:** 2026-10-03

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1397.
Builds on ADR-0009 (TLS matches the platform's build), ADR-0061 and ADR-0190 (an HTTPS
proxy is tunnelled over its own TLS handshake and offers `http/1.1` by ALPN), ADR-0159
(HTTP/2 over `IConnection`), ADR-0223 and ADR-0250 (HTTP/3 through a proxy) and ADR-0137
(a real curl option not yet implemented is refused as the installed libcurl does).

## Context

`--proxy-http2` and `--proxy-http3` are the only options of curl 8.21.0's release option
table (`src/tool_getparam.c` lines 252-253) with no row in
`Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`. They exist only in
`CurlOptionAliasTable.cs` and `CurlHelpTable.cs`, so Curl refuses both with
`curl: option --proxy-http2: the installed libcurl version does not support this` and exit 2.

In curl 8.21.0 (`src/tool_getparam.c` lines 2030-2046) `--proxy-http2` needs the
`HTTPS-proxy` and `HTTP2` features and sets `CURLPROXY_HTTPS2`; `--no-proxy-http2` sets
`CURLPROXY_HTTPS`. `--proxy-http3` is refused unless libcurl was built with
`USE_PROXY_HTTP3`, and then needs `HTTPS-proxy` and `HTTP3` and sets `CURLPROXY_HTTPS3`.

Measured on 2026-10-03:

| Build | `-V` features | `--proxy-http2` | `--proxy-http3` |
| --- | --- | --- | --- |
| curl 8.21.0 mingw, Schannel (Windows reference, ADR-0018) | `HTTPS-proxy`, no `HTTP2` | refused, exit 2, the ADR-0137 line | refused, exit 2, the ADR-0137 line |
| curl 8.18.0 OpenSSL 3.5.5, nghttp2 1.68.0 (Ubuntu, WSL) | `HTTP2 HTTPS-proxy`, no `HTTP3` | accepted (`-x https://127.0.0.1:1 http://example.test/` reached the connect and failed with exit 7) | `curl: option --proxy-http3: is unknown`, exit 2 (8.18.0 predates the option) |

The Linux and macOS OpenSSL builds the standing rules name ship nghttp2 and no HTTP/3
backend (no `HTTP3` in `-V`), and `USE_PROXY_HTTP3` is an opt-in build define that
distribution builds do not set. Without `HTTP3` an 8.21.0 build would refuse
`--proxy-http3` even if it were defined.

## Decision

1. **`--proxy-http2` on Windows keeps the ADR-0137 refusal** (exit 2, unchanged text), as
   the Schannel reference build does, because it has no `HTTP2` feature.
2. **`--proxy-http2` off Windows is accepted** and `--no-proxy-http2` turns it back off,
   as the OpenSSL builds do. The option only matters with an `https://` proxy; with any
   other proxy type, or no proxy, it changes nothing, as `CURLPROXY_HTTPS2` does in curl.
3. **`--proxy-http3` stays refused on every platform** with the ADR-0137 line and exit 2,
   because no reference build defines `USE_PROXY_HTTP3` and none has `HTTP3`. This is not
   a feature left out: it is the reference builds' own answer, the same ground ADR-0223
   stood on. If a reference build starts accepting it, a new ADR supersedes this point and
   the CONNECT tunnel over QUIC to the proxy is built on ADR-0250's QUIC connector.
4. **ALPN.** With `--proxy-http2` the proxy's TLS handshake offers `h2` then `http/1.1`
   instead of ADR-0190's lone `http/1.1`. When the proxy picks `h2`, the tunnel is an
   HTTP/2 `CONNECT` stream (RFC 9113 section 8.5: `:method CONNECT`, `:authority
   host:port`, no `:scheme` or `:path`, then `Proxy-Authorization` and `User-Agent` as
   HTTP/2 headers), and the target's bytes travel in that stream's DATA frames. When the
   proxy picks `http/1.1` or nothing, Curl falls back to today's HTTP/1.1 `CONNECT`
   tunnel, byte for byte as ADR-0061 and ADR-0190 pin it.
5. **Where it lives.** The tunnel belongs to `Curl.Networking.UnitLibrary`, beside the
   HTTP/1.1 tunnel in `TcpConnector` and `ConnectTunnelVerboseLines`. It frames the stream
   with `Curl.Http2.UnitLibrary`'s hand-built codec, which `Curl.Networking.UnitLibrary`
   gains a reference to (neither is a protocol library, so the protocol-isolation rule is
   kept). The tunnel stream is presented to the transfer as an `IConnection`, so
   `Curl.Protocol.Http.UnitLibrary` and every other protocol run over it unchanged.
   `Curl.Cli.UnitLibrary` parses the option and `Curl.Console` carries it to the connector.
6. **Output is measured before it is pinned.** Before any test pins them, real curl
   8.21.0 (or the newest OpenSSL build with nghttp2 at hand, 8.18.0 under WSL) is measured
   against `Record-CurlExchange.ps1` standing in for an HTTP/2 HTTPS proxy, for these
   `-v` lines: the proxy's `ALPN: curl offers h2,http/1.1` and the `ALPN: server accepted`
   line; the lines that announce the HTTP/2 tunnel (`CONNECT tunnel: HTTP/2 negotiated`
   or whatever curl prints) and the `[HTTP/2] [1] OPENED stream for` / `[:method: CONNECT]`
   / `[:authority: ...]` request lines; the proxy's `HTTP/2 200` reply and
   `CONNECT tunnel established, response 200`; a refused tunnel (`HTTP/2 407` and
   `HTTP/2 403`, with their exit codes); and the fallback when the proxy answers ALPN with
   `http/1.1`. The recorder cannot speak HTTP/2 today, so it gains an `-Http2` mode first.

## Consequences

- Scripts that use `--proxy-http2` on Linux and macOS work with Curl as with curl; on
  Windows they fail exactly as the Schannel build fails them.
- `Curl.Networking.UnitLibrary` takes a dependency on `Curl.Http2.UnitLibrary`, so a
  change there rebuilds and retests the connector.
- `--proxy-http3` costs nothing now, and is revisited only when a reference build changes.

## Alternatives considered

- **Accept `--proxy-http2` on Windows too.** Lost: the Schannel reference refuses it, and
  a drop-in replacement must fail where the platform's curl fails.
- **Build `--proxy-http3` now.** Lost: no reference build accepts it, so Curl accepting
  it would differ from every platform's curl.
- **Put the tunnel in `Curl.Protocol.Http.UnitLibrary`.** Lost: the connector, not the
  protocol, owns proxy tunnels, and protocol handlers never dial or wrap connections.
