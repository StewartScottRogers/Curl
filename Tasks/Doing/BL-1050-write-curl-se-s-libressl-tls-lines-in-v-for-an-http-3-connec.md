---
id: BL-1050
title: Write curl.se's LibreSSL TLS lines in -v for an HTTP/3 connect on Windows
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-734]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-09-30
completed:
---
# BL-1050 — Write curl.se's LibreSSL TLS lines in -v for an HTTP/3 connect on Windows

## Goal

On Windows, `curl -v --http3-only https://<host>/` writes the TLS lines curl.se's official (LibreSSL, ngtcp2) build writes between `Trying` and `Established connection`, instead of none.

## Context

- ADR-0144 (Consequences) decides it: on Windows the QUIC connect prints curl.se's LibreSSL lines, `SSL Trust Anchors:` naming the anchors Curl actually used; on Linux and macOS the OpenSSL lines the TCP path prints. BL-734 found Windows prints none: `QuicDialer.Connected` (`Curl.Networking.UnitLibrary`) reports a `TlsHandshakeEvent`, but `VerboseTransferEventWriter` words it with `PlatformTlsBackend.ForProcess`, which is Schannel on Windows and prints only ALPN lines (and QUIC offers none).
- Measured by BL-734 (curl 8.18.0, LibreSSL 4.2.1, ngtcp2 1.21.0, `-s -v -i --http3-only https://cloudflare-quic.com/`), the lines after `Trying <ip>:443...`:
  ```
  * SSL Trust Anchors:
  *   CA Blob from configuration
  * SSL connection using TLSv1.3 / TLS_AES_256_GCM_SHA384 / [blank] / UNDEF
  * Server certificate:
  *   subject: CN=cloudflare-quic.com
  *   start date: <date> GMT
  *   expire date: <date> GMT
  *   issuer: C=US; O=Google Trust Services; CN=WE1
  *   Certificate level 0: Public key type ? (256/128 Bits/secBits), signed using ecdsa-with-SHA256
  *   Certificate level 1: Public key type ? (256/128 Bits/secBits), signed using ecdsa-with-SHA384
  *   Certificate level 2: Public key type ? (384/192 Bits/secBits), signed using ecdsa-with-SHA384
  *   subjectAltName: "cloudflare-quic.com" matches cert's "cloudflare-quic.com"
  * SSL certificate verified via OpenSSL.
  ```
  LibreSSL differs from OpenSSL 3 (`[blank] / UNDEF`, `Public key type ?`, no `OpenSSL verify result` line); the `Note: Using embedded CA bundle` lines are not printed (ADR-0144). The trust-anchor line names what Curl used (Windows store, `--cacert` file), so re-measure with `--cacert`.
- The event needs to say it came from a QUIC handshake (or carry the backend wording to use); `TlsHandshakeEvent` is in `Curl.Protocol.Abstractions.UnitLibrary`. Wording lives in `Curl.Output.UnitLibrary` (`OpenSslHandshakeText`, `TransferEventInfoText`).

## Acceptance criteria

- [ ] Re-measured with `Record-CurlExchange.ps1 -NoServer` and curl.se's build, with and without `--cacert`; output copied into Notes with varying parts marked.
- [ ] A test in `Curl.Output.UnitTests` pins the LibreSSL lines for a QUIC handshake event on Windows wording, and the OpenSSL lines are unchanged for a QUIC handshake on Linux wording.
- [ ] A test in `Curl.Networking.UnitTests` shows `QuicDialer` reports the handshake as a QUIC one.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
