---
id: BL-608
title: Check --pinnedpubkey against the server's public key, failing with exit 90
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-607]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-608 — Check --pinnedpubkey against the server's public key, failing with exit 90

## Goal

After the TLS handshake, the server certificate's SubjectPublicKeyInfo is compared with `--pinnedpubkey` (a PEM or DER public-key file, or `sha256//<base64>` hashes separated by `;`), and a mismatch fails with exit 90 (`CURLE_SSL_PINNEDPUBKEYNOTMATCH`) and curl 8.21.0's message, on each platform as its build does.

## Context

- Conformance audit 2026-09-28, row 17 (Major). Option: BL-607.
- Code: `Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs`, `TlsFailureMessages.cs`. `X509Certificate2.PublicKey.ExportSubjectPublicKeyInfo()` and `SHA256` are in the BCL.
- Whether the Schannel build supports file pins and hash pins, and whether `-k` skips the check, must be measured.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Tls -k`: the right `sha256//` hash (compute it from the script's certificate), a wrong hash, a list with the right one second, a PEM key file, a missing file; stderr and exit code copied into Notes, per platform where they differ.
- [ ] `Curl.Networking.UnitTests` pin each measured case with a generated test certificate, per platform with `OSCondition` where they differ.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
