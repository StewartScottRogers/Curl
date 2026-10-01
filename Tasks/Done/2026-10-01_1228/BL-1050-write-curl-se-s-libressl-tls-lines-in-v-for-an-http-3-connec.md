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
completed: 2026-10-01
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

- [x] Re-measured with `Record-CurlExchange.ps1 -NoServer` and curl.se's build, with and without `--cacert`; output copied into Notes with varying parts marked.
- [x] A test in `Curl.Output.UnitTests` pins the LibreSSL lines for a QUIC handshake event on Windows wording, and the OpenSSL lines are unchanged for a QUIC handshake on Linux wording.
- [x] A test in `Curl.Networking.UnitTests` shows `QuicDialer` reports the handshake as a QUIC one.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- 2026-10-01, measured with `Record-CurlExchange.ps1 -NoServer -Curl <WinGet curl.exe>` (curl 8.18.0, LibreSSL 4.2.1, ngtcp2 1.21.0), `-s -v -o NUL --http3-only https://cloudflare-quic.com/`; `<...>` marks the parts that vary. The `Note: Using embedded CA bundle` lines before `Host` are left out (ADR-0144).
  ```
  *   Trying <ip>:443...
  * SSL Trust Anchors:
  *   CA Blob from configuration                     <- no CA option
  *   Native: Windows System Stores ROOT+CA          <- --ca-native (before the blob line)
  *   CAfile: <path given to --cacert>               <- --cacert, instead of the blob line
  * SSL connection using TLSv1.3 / TLS_AES_256_GCM_SHA384 / [blank] / UNDEF
  * Server certificate:
  *   subject: CN=cloudflare-quic.com
  *   start date: <Sep 20 17:51:11 2026> GMT
  *   expire date: <Dec 19 18:51:00 2026> GMT
  *   issuer: C=US; O=Google Trust Services; CN=WE1
  *   Certificate level 0: Public key type ? (256/128 Bits/secBits), signed using ecdsa-with-SHA256
  *   Certificate level 1: Public key type ? (256/128 Bits/secBits), signed using ecdsa-with-SHA384
  *   Certificate level 2: Public key type ? (384/192 Bits/secBits), signed using ecdsa-with-SHA384
  *   subjectAltName: "cloudflare-quic.com" matches cert's "cloudflare-quic.com"
  * SSL certificate verified via OpenSSL.
  * Established connection to cloudflare-quic.com (<ip> port 443) from <local ip> port <port> 
  ```
  With `--cacert` (the Windows root store exported to PEM) the chain is built to that file's root: level 2 `Public key type ? (384/192 ...), signed using sha256WithRSAEncryption`, level 3 `Public key type ? (2048/112 ...), signed using sha1WithRSAEncryption`. With `-k`: `SSL Trust: peer verification disabled`, no `subjectAltName` line, and ` SSL certificate verification failed, continuing anyway!` (with its leading space) instead of the verified line.
- Decision (ADR-0297): `TlsHandshakeEvent.IsQuic` and `TlsTrustEvent.IsQuic` mark the QUIC connect's events; under Schannel wording they get the OpenSSL trust lines and `OpenSslHandshakeText.LibreSslLines`. Without `--cacert` on Windows Curl verifies against the Windows stores, so `TlsTrustEvent.UsesWindowsSystemStores` writes `Native: Windows System Stores ROOT+CA` (the `--ca-native` wording) instead of the embedded bundle's `CA Blob from configuration`, which Curl does not have.
- Tests: `VerboseTransferEventWriterQuicTlsTests` (Output) pins the LibreSSL lines, the trust lines, `-k`, unchanged Linux OpenSSL lines and unchanged Schannel TCP lines; `TcpConnectorQuicTests.TlsWording` (Networking) shows `QuicDialer` reports trust and handshake as QUIC, and which anchors it names per build.
- Gates: `dotnet build Curl.slnx -warnaserror` clean; fast tests green; `Measure-CodeQuality.ps1` 100% line and branch, 0 failing members, for Curl.Output, Curl.Networking and Curl.Protocol.Abstractions.

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. On Windows -v for an HTTP/3 connect writes curl.se's LibreSSL TLS lines, naming the Windows stores or the --cacert file
