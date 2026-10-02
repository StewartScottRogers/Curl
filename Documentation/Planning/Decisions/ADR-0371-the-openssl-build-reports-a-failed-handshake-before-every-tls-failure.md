# ADR-0371 — The OpenSSL build reports a failed handshake before every TLS failure

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1178.
Amends ADR-0363 (BL-1149), which left the OpenSSL build reporting a failed `TlsHandshakeEvent`
only for an `SslStreamTlsProvider` pin refusal.

## Context

Measured 2026-10-02 with curl 8.18.0, OpenSSL 3.5.5, in WSL against
`Record-CurlExchange.ps1 -Tls -ListenAddress 172.26.96.1` (a self-signed certificate for IP
127.0.0.1, TLS 1.2), `-v` and no `-k`:

- Host name mismatch (`https://172.26.96.1:47811/`), exit 60: `ALPN: curl offers h2,http/1.1`,
  the TLS message lines, `SSL connection using TLSv1.2 / ECDHE-RSA-AES256-GCM-SHA384 / ...`,
  `ALPN: server did not agree on a protocol. Uses default.`, `Server certificate:` and its
  details, `Certificate level 0: ...`, ` subjectAltName does not match ipv4 address 172.26.96.1`,
  then the error `SSL: no alternative certificate subject name matches target ipv4 address ...`.
- Untrusted root, host matching (`--connect-to 127.0.0.1:47812:172.26.96.1:47812
  https://127.0.0.1:47812/`), exit 60: the same lines to `Certificate level 0`, then
  `  subjectAltName: "127.0.0.1" matches cert's IP address!`, then the error
  `SSL certificate OpenSSL verify result: self-signed certificate (18)`. No
  `OpenSSL verify result:` line and no `continuing anyway!` line.
- `-k --tlsv1.3` against the TLS 1.2 server, exit 35: `ALPN: curl offers h2,http/1.1` before the
  ClientHello line, then the trust and alert lines and the error. No `SSL connection using`.

## Decision

- `SslStreamTlsProvider` reports a failed event in the OpenSSL build too, for every handshake
  failure: what was negotiated when the certificate was judged when the certificate or the pin
  was refused, and nothing negotiated (`SslProtocols.None`) otherwise.
- `OpenSslHandshakeText` words a failed event that negotiated nothing as the ALPN offer alone, and
  leaves out the verify result and the hash line of a failed event whose certificate was refused
  with the host name checked (no `-k`) and a non-zero verify result, since curl's exit 60 message
  is that result.
- `Curl.Tls` hands the verifier the version, suite and ALPN protocol with the chain
  (`ServerCertificateChain.ProtocolVersion`, `CipherSuite`, `ApplicationProtocol`), and
  `HandBuiltTlsProvider`'s OpenSSL build reports a failed event built from them on a pin refusal.
- Every failed event carries the pin's hash, so `PeerVerification.ReportPinnedPublicKeyRefusal`
  no longer writes the hash line.

## Consequences

- The OpenSSL build's `-v` now prints the certificate lines before an exit 60 and the ALPN offer
  before an exit 35, as measured. Curl does not report TLS message lines from `SslStream`, so
  those stay missing in both cases.
- `HandBuiltTlsProvider`'s OpenSSL build still reports nothing of the handshake before its own
  exit 60 or exit 35; only the pin refusal was asked for here.

## Alternatives considered

- Keeping the version and suite on `HandBuiltHandshake.Failed`: the handshake classes would have
  to carry their state out of every failure path, where the verifier sees it at one point.
