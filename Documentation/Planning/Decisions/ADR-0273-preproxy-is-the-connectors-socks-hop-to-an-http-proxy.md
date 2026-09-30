# ADR-0273 — `--preproxy` is the connector's SOCKS hop to an HTTP proxy, and a lone pre-proxy is the transfer's SOCKS proxy

- **Status:** Accepted
- **Date:** 2026-09-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-614.

## Context

`--preproxy [scheme://]host[:port]` names a SOCKS proxy curl passes through before the HTTP
proxy `-x` names. Measured 2026-09-30 against curl 8.21.0 (Schannel) with
`Record-CurlExchange.ps1 -Script` playing a SOCKS5 server (BL-614 Notes has every command and
byte):

- `--preproxy socks5://S -x http://10.0.0.1:3128 http://h/` sends the SOCKS5 greeting and a
  request for `10.0.0.1:3128`, then the forwarded `GET http://h/` inside the tunnel; with
  `https://h/` it sends the `CONNECT h:443` inside it.
- A refused SOCKS request is exit 97 `cannot complete SOCKS5 connection to 10.0.0.1. (5)`; a
  refused dial is exit 7 `Failed to connect to 10.0.0.1:3128 over proxy <S host> ...`; an
  unresolvable pre-proxy is exit 5 `Could not resolve proxy: <S host>`.
- With no `-x` the pre-proxy is the SOCKS proxy for the URL's host, and the proxy environment
  variables are not read. No scheme means SOCKS4.
- `--noproxy` matching the host drops both.
- An `http://` or `https://` pre-proxy is exit 5 `Unsupported pre-proxy type for '<text>'`,
  checked before `-x`; a SOCKS `-x` (or `--socks5`) beside a pre-proxy is exit 5 `Having a
  SOCKS pre-proxy and proxy is not supported with '<-x text>'`.

## Decision

1. The pre-proxy reaches `TcpConnector` as a constructor argument (`preProxy`), built once per
   option group by `CurlComposition.PreProxyOf`, not as a new `ConnectTarget` property. It is
   one value for every transfer of the group, so no contract in
   `Curl.Protocol.Abstractions.UnitLibrary` changes and no protocol handler learns of it.
2. The connector uses it only on the way to an HTTP or HTTPS proxy: for a tunnelling
   `ConnectTarget.Proxy` of kind `Http`, `Http10` or `Https` it resolves and dials the
   pre-proxy, runs the SOCKS handshake to the proxy's host and port, and then runs the
   CONNECT (after the proxy's TLS for `Https`) on that connection; a forward-proxy target is
   opened as a SOCKS tunnel through the pre-proxy to the forward proxy. A direct target and a
   SOCKS `ConnectTarget.Proxy` never use it.
3. `ConnectionPoolKey` is unchanged. Each option group builds its own connector and pool, and
   every connection to an HTTP proxy that pool holds went through the same pre-proxy, so the
   key cannot mix connections made with and without one.
4. `TransferProxySelection` makes the per-transfer choices: `--noproxy`, the two exit 5
   refusals, and a lone pre-proxy becoming `ConnectTarget.Proxy` itself, which the existing
   SOCKS path opens. `-U` replaces the credential of whichever proxy is chosen, the lone
   pre-proxy included, as libcurl copies the proxy user to the SOCKS proxy when there is no
   HTTP proxy.

## Consequences

- The measured request bytes and every failure match curl 8.21.0 through fakes
  (`TcpConnectorTests.PreProxy`, `TransferProxySelectionPreProxyTests`).
- curl's `-v` line `Opened SOCKS connection from ... to ... (via ...)` is not printed for any
  SOCKS tunnel yet, with or without a pre-proxy.

## Alternatives considered

- **A `ConnectTarget.PreProxy` property.** It would let one run mix pre-proxies per transfer,
  which curl's command line cannot ask for, and it changes the shared contract every protocol
  task depends on.
- **Wrapping the dialer in a SOCKS-dialling `ITcpDialer`.** The dialer sees addresses, not the
  HTTP proxy's name, so SOCKS5h and SOCKS4a could not send the name, and exit 7 could not name
  the proxy as curl does.
