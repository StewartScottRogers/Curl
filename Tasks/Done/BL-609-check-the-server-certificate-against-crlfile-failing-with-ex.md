---
id: BL-609
title: Check the server certificate against --crlfile, failing with exits 82 and 60
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-607]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Record-CurlExchange.ps1, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-609 — Check the server certificate against --crlfile, failing with exits 82 and 60

## Goal

`--crlfile <file>` loads a PEM certificate revocation list; an unreadable or unparsable file fails with exit 82 (`CURLE_SSL_CRL_BADFILE`), and a server certificate the list revokes fails as the platform's curl 8.21.0 build fails it (exit 60 on OpenSSL builds; the Schannel build's answer measured).

## Context

- Conformance audit 2026-09-28, row 17 (Major). Option: BL-607.
- The BCL cannot hand a CRL file to `X509Chain`; the check can be done by parsing the CRL (`System.Formats.Asn1`, or `CertificateRevocationListBuilder.Load` in .NET 9+, both BCL) and matching the serial and issuer, and verifying the CRL's signature with the issuer's key. Record the approach in the XML docs.
- Code: `Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs`, `TlsFailureMessages.cs`; revocation today is ADR-0086.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Tls` and `--cacert` for the script's certificate: `--crlfile` missing, garbage, an empty CRL from that CA, and a CRL revoking the certificate; stderr and exit code copied into Notes, on each platform.
- [x] `Curl.Networking.UnitTests` pin each measured case with generated test CA, certificate and CRLs (`CertificateRevocationListBuilder`), per platform with `OSCondition` where they differ.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- `touches` gained `Record-CurlExchange.ps1` (it could not write a CRL, so it gained
  `-TlsEmptyCrlFile` and `-TlsRevokingCrlFile`, lists signed by the throwaway root, which may now
  sign CRLs) and `Documentation/Planning/Decisions` (ADR-0194). No task in Doing names either.
- Measured 2026-09-29, `-sS --cacert root.pem --crlfile <file>`, curl 8.21.0 Schannel (Windows)
  and curl 8.18.0 OpenSSL 3.5.5 (WSL Ubuntu, `-Curl wsl.exe -ListenAddress 172.26.96.1` with
  `--connect-to 127.0.0.1:18609:172.26.96.1:18609`):
  - missing file: exit 2, `curl: The file '<path>' provided to --crlfile does not exist` then
    `curl: option --crlfile: is badly used here`, both builds (the parser already did this).
  - garbage: Schannel exit 60 `schannel: the revocation status is unknown` (as with no
    `--crlfile`); OpenSSL exit 82 `curl: (82) error loading CRL file: <path>`. An empty file, a DER
    list and a directory: OpenSSL exit 82, the same text.
  - empty list from the root: Schannel exit 60 (as above); OpenSSL exit 0.
  - list revoking the server certificate: Schannel exit 60 (as above); OpenSSL exit 60
    `curl: (60) SSL certificate OpenSSL verify result: certificate revoked (23)` plus the help block.
  - OpenSSL, a list from another CA: exit 60 `... unable to get certificate CRL (3)`; a list in the
    root's name signed by another key: exit 60 `... CRL signature failure (8)`.
  - Schannel with `--ssl-no-revoke`: the revoking list and garbage both exit 0, so Schannel ignores
    `--crlfile` entirely. `-k` with the revoking list or garbage: exit 0 in both builds.
  - The builds differ, but the providers pin both builds through their build flag on every
    platform, so no `OSCondition` split was needed.
- Design (ADR-0194): the lists are decoded by hand (`CertificateRevocationList`, `System.Formats.Asn1`)
  because `CertificateRevocationListBuilder.Load` gives only the entries; the check runs in
  `ServerCertificateVerification.Judge` after `VerifyPeer` and before the pin, shared by both
  providers and the QUIC verifier; exit 82 travels as `CertificateRevocationListFileException`
  through the existing trust-anchor catch, sorted by `TrustAnchorsUnusable`.
- `--proxy-crlfile` stays with BL-611.
- Quality: Curl.Networking.UnitLibrary 100% line and branch, 0 failing members (656); Curl.Console
  100% and 100%, 0 failing (605).

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --crlfile loads PEM CRLs and refuses a revoked server certificate with exit 60 and a bad file with exit 82 in the OpenSSL build; the Schannel build ignores it, as curl.exe does
