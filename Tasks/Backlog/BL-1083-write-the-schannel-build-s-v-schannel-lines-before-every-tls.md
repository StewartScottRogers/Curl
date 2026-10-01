---
id: BL-1083
title: Write the Schannel build's -v schannel: lines before every TLS handshake
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-10-01
completed:
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

- [ ] `TransferEventInfoText.TlsTrust` under `TlsBackend.Schannel` returns the measured `schannel:` lines; a test in Curl.Output.UnitTests pins each measured case.
- [ ] `CurlCommandRunnerSmtpTransferEventTests` pins the STARTTLS case with both `schannel:` lines before the second `Established connection` line.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-10-01: Created.
