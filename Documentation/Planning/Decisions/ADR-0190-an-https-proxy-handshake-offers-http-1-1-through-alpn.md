# ADR-0190 — An HTTPS proxy's handshake offers `http/1.1` through ALPN

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-753.
Amends ADR-0124, which left the proxy handshakes offering nothing because curl's proxy ALPN
had not been measured.

## Context

`TcpConnector` runs a TLS handshake with an HTTPS proxy in two places: the proxy a CONNECT
tunnel runs through (`-x https://... -p`, or any `https://` target), and an HTTPS forward proxy
(`-x https://... http://...`), which is the connection's target itself. Until now both offered
no ALPN.

Measured with `Record-CurlExchange.ps1 -Tls` standing in for the proxy on 2026-09-29, curl
8.21.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux (WSL, `-ListenAddress`), with
`curl -v --proxy-insecure -x https://<proxy> http://example.test/` (the full lines are in
BL-753's Notes):

| Case | Schannel | OpenSSL |
| --- | --- | --- |
| forward proxy | `ALPN: curl offers http/1.1` | `ALPN: curl offers http/1.1` |
| forward proxy, `--http2` | — | `ALPN: curl offers http/1.1` |
| tunnel (`-p`) | `ALPN: curl offers http/1.1` | `ALPN: curl offers http/1.1` |
| either, `--no-alpn` | no `ALPN:` line | no `ALPN:` line |

Both builds follow the offer with `ALPN: server did not agree on a protocol. Uses default.`
when the proxy picks nothing, and a tunnel adds `CONNECT: no ALPN negotiated` before
`Establishing HTTP proxy tunnel`.

## Decision

1. **Both proxy handshakes offer `http/1.1` alone** (`HttpApplicationProtocols.Http11Only`),
   whatever the HTTP version options say: `--http2` changes the origin's offer (ADR-0141) and
   never the proxy's.
2. **`--no-alpn` suppresses the proxy's offer too**, as measured. The TLS providers already drop
   the offer when their `TlsClientOptions.UseAlpn` is off; the proxy's provider is built from
   `TlsClientOptionsMapping.ProxyFromCommandLine` in `Curl.Console`, which does not carry
   `--no-alpn` yet. That mapping is a follow-up task, since `Curl.Console` is outside BL-753.

## Consequences

- `-v` through an HTTPS proxy prints curl's `ALPN: curl offers http/1.1` for the proxy's
  handshake on every platform.
- Until the follow-up lands, `--no-alpn` with an HTTPS proxy still offers `http/1.1` to the
  proxy where curl offers nothing.
- The `CONNECT: no ALPN negotiated` line is not printed yet; it is its own follow-up.

## Alternatives considered

- **Offer the origin's list to the proxy** (`h2,http/1.1` under `--http2`): contradicts the
  OpenSSL measurement.
- **Keep offering nothing:** contradicts both builds.
