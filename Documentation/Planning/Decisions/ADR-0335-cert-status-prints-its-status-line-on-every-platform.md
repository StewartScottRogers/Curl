# ADR-0335 — `--cert-status` prints `* SSL certificate status:` under `-v` on every platform

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-875.
This completes ADR-0191's decision 5, which left the line out.

## Context

curl's `verifystatus()` (`lib/vtls/openssl.c`) prints `SSL certificate status: <name> (<code>)`
once it has found the server's certificate in a stapled OCSP response that is still current:
`good (0)`, `revoked (1)` or `unknown (2)`, OpenSSL's `OCSP_cert_status_str` names. A revoked or
unknown status then fails with exit 91. A response that is missing, malformed, unsuccessful,
badly signed, without the certificate or expired fails before the line. Measured on 2026-09-29
(BL-610) with curl 8.18.0 on OpenSSL 3.5.5: `* SSL certificate status: good (0)` after
`* SSL certificate verified via OpenSSL.`, and `* SSL certificate status: revoked (1)` before
the revocation reason. curl.se's Windows build (LibreSSL) runs the same code. The Schannel build
ignores `--cert-status`.

The Schannel build's `* schannel: enabled automatic use of client certificate` (or `disabled`)
line was measured again on 2026-10-01 with `Record-CurlExchange.ps1 -Tls -k` (curl 8.21.0,
`C:\Windows\System32`): it comes after `Trying` and before `ALPN: curl offers` for every https
transfer, with or without `-k`. `SchannelTrustText` has printed it since BL-1083, from
`TlsTrustEvent.UsesAutomaticClientCertificate`, so it needs nothing more.

## Decision

1. **`HandBuiltTlsProvider` reports the status line through `ITransferEvents.ReportInfo`**
   (`CertificateStatusText.Report`), not as a new `TlsHandshakeEvent` field. The line is only
   ever one fixed text per status, it is printed whether the handshake completes or fails, and a
   failed handshake reports no `TlsHandshakeEvent`. A completed handshake reports it right after
   the handshake event, so it follows `SSL certificate verified via OpenSSL.`; a rejected one
   reports it before the exit 91 failure.
2. **Every platform prints it, the Schannel text set included**, because ADR-0191 already takes
   `--cert-status`'s behaviour and texts from the builds that support the option (OpenSSL and
   curl.se's LibreSSL build) on every platform.

## Consequences

- No change to `Curl.Protocol.Abstractions` or `Curl.Output`: the line reaches `-v` and
  `--trace` as any other info line does.
- A rejected revoked response prints the status line but not the certificate lines the OpenSSL
  build prints before it, because the hand-built provider reports no handshake event for a
  failed handshake. That was already true before this decision.

## Alternatives considered

- **A `CertificateStatus` field on `TlsHandshakeEvent`, rendered by `OpenSslHandshakeText`.**
  This changes the shared contract and two more projects. It also cannot carry the revoked case,
  which fails before any handshake event exists.
- **Print the line only in the OpenSSL text set.** On Windows, a `--cert-status` failure would
  print exit 91 with no status line, while the LibreSSL build that defines this behaviour on
  Windows prints one.
