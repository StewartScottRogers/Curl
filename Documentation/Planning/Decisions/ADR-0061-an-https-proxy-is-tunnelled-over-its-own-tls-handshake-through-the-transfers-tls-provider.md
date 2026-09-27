# ADR-0061 — An HTTPS proxy is tunnelled over its own TLS handshake, through the transfer's TLS provider until the --proxy-* options land

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (task BL-266, 2026-09-27).

## Context

`TcpConnector` tunnelled through HTTP and SOCKS proxies and threw `NotSupportedException`
for `ProxyKind.Https`. curl 8.21.0 (the Schannel reference build, ADR-0009) was measured
against a TLS loopback proxy; the commands are in BL-266's Notes. It:

- runs TLS to the proxy host first, then sends inside it exactly the CONNECT bytes it sends
  to an HTTP proxy, then, for an https target, runs a second handshake to the target
  inside the tunnel;
- reports a failed handshake to the proxy with the same exit code and message as one to a
  target (exit 35 `Recv failure: Connection was aborted`, exit 60 `schannel:
  SEC_E_UNTRUSTED_ROOT ...`), a refused CONNECT as `(7) CONNECT tunnel failed, response
  407`, and a failed target handshake as the usual exit 35;
- reports `%{time_appconnect}` as `0` for an http target through an HTTPS proxy;
- does not apply `-k` to the proxy: a self-signed proxy fails with exit 60 under `-k` and
  succeeds only with `--proxy-insecure`.

The `--proxy-*` TLS options are not parsed yet.

## Decision

1. `TcpConnector` resolves and dials an HTTPS proxy as any other, runs the proxy handshake
   keyed on the proxy host, then reuses the HTTP-proxy CONNECT path over the secured
   connection, and applies target TLS inside it when `UseTls` is set. Every handshake
   failure is the TLS provider's result, returned as it is.
2. The proxy handshake runs through the same injected `ITlsProvider` as the target's, so
   the proxy is verified with the transfer's TLS settings. This differs from curl only
   when `-k`, `--cacert` or `--capath` is given. A separate proxy provider seam waits for
   BL-362, which parses the `--proxy-*` TLS options and wires both ends at once
   (`Curl.Console`'s composition tests pin `TcpConnector`'s single `ITlsProvider` field,
   so the seam and its wiring belong in one change).
3. `ConnectTimings.TlsHandshakeCompleted` and `ConnectResult.PeerCertificates` come only
   from the target's handshake, matching `%{time_appconnect}` of `0` for an http target.

## Consequences

- No `ProxyKind` makes `TcpConnector` throw `NotSupportedException`.
- BL-328 can remove the console's exit 4 for HTTPS proxies; BL-362 gives the proxy its own
  TLS options.
