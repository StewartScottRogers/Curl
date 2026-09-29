---
id: BL-609
title: Check the server certificate against --crlfile, failing with exits 82 and 60
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-607]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-609 — Check the server certificate against --crlfile, failing with exits 82 and 60

## Goal

`--crlfile <file>` loads a PEM certificate revocation list; an unreadable or unparsable file fails with exit 82 (`CURLE_SSL_CRL_BADFILE`), and a server certificate the list revokes fails as the platform's curl 8.21.0 build fails it (exit 60 on OpenSSL builds; the Schannel build's answer measured).

## Context

- Conformance audit 2026-09-28, row 17 (Major). Option: BL-607.
- The BCL cannot hand a CRL file to `X509Chain`; the check can be done by parsing the CRL (`System.Formats.Asn1`, or `CertificateRevocationListBuilder.Load` in .NET 9+, both BCL) and matching the serial and issuer, and verifying the CRL's signature with the issuer's key. Record the approach in the XML docs.
- Code: `Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs`, `TlsFailureMessages.cs`; revocation today is ADR-0086.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Tls` and `--cacert` for the script's certificate: `--crlfile` missing, garbage, an empty CRL from that CA, and a CRL revoking the certificate; stderr and exit code copied into Notes, on each platform.
- [ ] `Curl.Networking.UnitTests` pin each measured case with generated test CA, certificate and CRLs (`CertificateRevocationListBuilder`), per platform with `OSCondition` where they differ.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
