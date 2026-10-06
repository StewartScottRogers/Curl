---
id: BL-1083
title: Write the Schannel build's -v schannel: lines before every TLS handshake
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console.UnitTests, Curl.Protocol.Abstractions.UnitLibrary]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1083 — Write the Schannel build's -v schannel: lines before every TLS handshake

## Goal

Under the Schannel wording (Windows), `-v` writes the `* schannel:` lines curl 8.21.0 writes before a TLS handshake - for HTTPS, LDAPS and every in-place `STARTTLS` upgrade alike - byte for byte.

## Context

- Found by BL-1058. curl 8.21.0 (mingw, Schannel) writes, before the handshake of `-v -k --ssl-reqd ... smtp://127.0.0.1:18027/client` after `< 220 Ready to start TLS`:
  ```
  * schannel: disabled automatic use of client certificate
  * schannel: using IP address, SNI is not supported by OS.
  ```
  ADR-0046 records `* schannel: disabled automatic use of client certificate` before the ALPN lines of `curl -v https://example.com/` too. Curl writes neither line today, for any scheme.
- The natural seam is the `TlsTrustEvent` every reporting provider reports before its handshake (`ITransferEvents.ReportTlsTrust`): `TransferEventInfoText.TlsTrust` (Curl.Output.UnitLibrary) words it for the OpenSSL build and returns nothing for Schannel. The event may need to carry whether a client certificate is configured and whether the target host is an IP literal.
- Measure with `Record-CurlExchange.ps1` before pinning: what curl writes for a host name rather than an IP address (SNI), what replaces the first line with `--cert`, and the three `schannel:` renegotiation lines ADR-0046 saw after an HTTPS request.
- Then add the two lines to `CurlCommandRunnerSmtpTransferEventTests.RunAsync_VerboseMailUploadWithStartTls_WritesTheEstablishedConnectionLineAgainAfterTheUpgrade` (Curl.Console.UnitTests), whose comment names this task; that test's TLS provider reports nothing, so it needs one that reports its trust.

## Acceptance criteria

- [x] `TransferEventInfoText.TlsTrust` under `TlsBackend.Schannel` returns the measured `schannel:` lines; a test in Curl.Output.UnitTests pins each measured case.
- [x] `CurlCommandRunnerSmtpTransferEventTests` pins the STARTTLS case with both `schannel:` lines before the second `Established connection` line.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measured curl 8.21.0 (mingw, Schannel) with `Record-CurlExchange.ps1 -Reset` (the server resets each connection, so curl writes its pre-handshake lines and fails with 35):
  - `-v -k https://127.0.0.1:18431/` and `-v https://127.0.0.1:18431/`: `* schannel: disabled automatic use of client certificate`, `* schannel: using IP address, SNI is not supported by OS.`, then `* ALPN: curl offers http/1.1`.
  - `-v -k https://localhost:18431/` (host name): only the first line. `--no-alpn` changes neither.
  - `--ssl-auto-client-cert`: the first line becomes `* schannel: enabled automatic use of client certificate`.
  - `--cert nosuch.pem`: the `disabled` line, then `* schannel: Failed to get certificate location or file for nosuch.pem`, exit 58 - Curl reports the trust only after the certificate loads, so that case is filed as BL-1088.
  - IPv6 literal: the script binds IPv4 only, so not measured; curl's `schannel_connect_step1` tests both `AF_INET` and `AF_INET6` with `inet_pton`, so `[::1]` is treated as an IP address too.
- Design: `TlsTrustEvent` gains `UsesAutomaticClientCertificate` and `TargetsIpAddress`, set by `SslStreamTlsProvider.DescribeTrust(options, targetHost)` (shared by `HandBuiltTlsProvider` and `QuicDialer`); `SchannelTrustText` (Curl.Output.UnitLibrary) words them, and `TransferEventInfoText.TlsTrust` uses it for the Schannel wording outside QUIC (QUIC keeps curl.se's LibreSSL wording, ADR-0144). The proxy handshake gets the proxy's options, so `--proxy-ssl-auto-client-cert` drives its line.
- `touches`: added Curl.Protocol.Abstractions.UnitLibrary, because `TlsTrustEvent` lives there; no task in Doing on `origin/work/dark-factory` names it (only BL-1035, Curl.Protocol.Ssh.*).
- The three `schannel:` renegotiation lines ADR-0046 saw after an HTTPS response are post-handshake, outside this task's goal; filed as BL-1089.
- Gates: `dotnet build Curl.slnx -warnaserror` clean; fast tests all green; `Measure-CodeQuality.ps1 -Library` reports 0 failing members for Curl.Output.UnitLibrary, Curl.Networking.UnitLibrary and Curl.Protocol.Abstractions.UnitLibrary.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. -v under the Schannel wording writes the measured schannel: client-certificate and SNI lines before every TLS handshake
