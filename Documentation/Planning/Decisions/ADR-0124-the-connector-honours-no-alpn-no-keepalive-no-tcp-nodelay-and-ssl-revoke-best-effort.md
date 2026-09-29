# ADR-0124 — The connector honours --no-alpn, --no-keepalive, --no-tcp-nodelay and --ssl-revoke-best-effort; --ca-native changes nothing

- **Status:** Accepted; the proxy handshake's ALPN is amended by [ADR-0190](ADR-0190-an-https-proxy-handshake-offers-http-1-1-through-alpn.md)
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-490.

## Context

BL-489 parses five switches that change the TCP and TLS layers. BL-490 makes them act, and
measured each one first with `Record-CurlExchange.ps1 -Tls` (extended with
`-TlsRootCertificateFile`, which serves a leaf issued by a throwaway private root with no
revocation endpoint and writes the root's PEM for `--cacert`). Windows is curl 8.21.0
(mingw, Schannel); the OpenSSL build is curl 8.18.0 (OpenSSL 3.5.5) on Ubuntu under WSL,
reached through `-ListenAddress` and `--connect-to`.

| Command line | Schannel | OpenSSL |
| --- | --- | --- |
| `-v -k https://127.0.0.1:P/` | `ALPN: curl offers http/1.1`, `ALPN: server did not agree on a protocol. Uses default.` | `ALPN: curl offers h2,http/1.1`, the same answer line after `SSL connection using` |
| `-v -k --no-alpn https://...` | no `ALPN:` line | no `ALPN:` line |
| `--cacert root.pem https://...` | exit 60, `schannel: the revocation status is unknown` | exit 0 |
| `--cacert root.pem --ssl-revoke-best-effort` | exit 0 | exit 0 |
| `--cacert root.pem --ca-native` | exit 60, as without it | exit 0, as without it |
| `--ca-native` (private root not in any store) | exit 60 | exit 60, same `SSL Trust Anchors` lines as without it |

## Decision

1. **ALPN.** A handshake to the origin of an `https://` transfer (the target the HTTP
   handler pools as `https`) offers `http/1.1`, on every platform, and the `TlsHandshakeEvent`
   carries the offer and the server's selection, so `-v` prints the Schannel build's two
   `ALPN:` lines. `--no-alpn` (`TlsClientOptions.UseAlpn`) sends no ALPN extension. Other
   protocols, a forward proxy and the handshake with an HTTPS proxy offer nothing, as before.
   The OpenSSL build's `h2,http/1.1` waits for HTTP/2: BL-655 decides each platform's list
   and BL-659 offers it; until then offering `h2` would let a server pick a protocol Curl
   cannot speak.
2. **Socket options.** `TcpDialer` sets `TCP_NODELAY` and `SO_KEEPALIVE` (probe time and
   interval 60 seconds, curl's `--keepalive-time` default) on each socket before it connects,
   from `TcpSocketOptions`; `--no-tcp-nodelay` and `--no-keepalive` turn each off. The
   setting lives in `TcpDialer.ApplySocketOptions`, measured by unit tests on an unconnected
   socket, so `DialAsync` stays the thin adapter ADR-0083 excludes.
3. **`--ssl-revoke-best-effort`.** The Schannel build still checks revocation for a
   `--cacert` chain (ADR-0086) but accepts a chain whose every fault is
   `RevocationStatusUnknown` or `OfflineRevocation`, as curl masks those two trust errors.
   The OpenSSL build never checks revocation, so ignores it.
4. **`--ca-native` changes nothing.** Without `--cacert` the provider already verifies
   against the operating system's store (`SslStream`'s default), and with it both builds
   still verify against the file, so the option maps to no setting.

## Consequences

- `IHandshakeReportingTlsProvider` takes the protocols to offer; `TcpConnector` decides
  them per target (`ApplicationProtocolsFor`), the provider drops them under `--no-alpn`.
- Whether curl offers ALPN to an HTTPS proxy was not measured; the proxy handshake offers
  none, as before.
