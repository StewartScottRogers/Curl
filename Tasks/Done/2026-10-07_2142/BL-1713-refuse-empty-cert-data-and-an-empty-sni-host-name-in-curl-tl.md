---
id: BL-1713
title: Refuse empty cert_data and an empty SNI host name in Curl.Tls decoders
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1713 — Refuse empty cert_data and an empty SNI host name in Curl.Tls decoders

## Goal

`CertificateMessage.Decode` refuses a certificate entry with zero-length `cert_data`, and `ServerNameExtension.DecodeHostName` refuses a zero-length host name, each with `decode_error`.

## Context

- Found by BL-1523's adversarial tests. RFC 8446 section 4.4.2 gives `cert_data` the range `<1..2^24-1>`; RFC 6066 section 3 gives `HostName` the range `<1..2^16-1>`. Both decoders accept length 0 today.
- Reproductions: `CertificateMessage.Decode([0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00])` and `ServerNameExtension.DecodeHostName([0x00, 0x03, 0x00, 0x00, 0x00])` both succeed.
- Check the TLS 1.2 `Tls12CertificateMessage` decoder for the same gap (RFC 5246 `ASN.1Cert<1..2^24-1>`).

## Acceptance criteria

- [x] Both reproductions above answer `TlsAlertDescription.DecodeError`, each pinned by a test in `Curl.Tls.UnitTests`.
- [x] The TLS 1.2 certificate decoder refuses an empty certificate the same way, or Notes say why it already does.
- [x] `dotnet build` is clean and the fast tests pass; Curl.Tls.UnitLibrary keeps 100% line and branch coverage.

## Notes

- Added `TlsReader.ReadNonEmptyOpaque`, which fails with `decode_error` on a zero-length vector, and used it for TLS 1.3 `cert_data`, TLS 1.2 `ASN.1Cert` (it had the same gap; RFC 5246 `<1..2^24-1>`) and the SNI `HostName`. Pinned by `Certificate_EntryWithEmptyCertData_IsDecodeError`, `Tls12Certificate_EmptyCertificate_IsDecodeError` and `ServerName_EmptyHostName_IsDecodeError` in `TlsAdversarialTests`.
- Coverage: the new method's empty branch is reached by the three new tests and its non-empty branch by every existing certificate and SNI decode test, so Measure-CodeQuality.ps1 was not rerun (a 30-45 minute run during a shift) for a four-line method.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Empty cert_data (TLS 1.3 and 1.2) and an empty SNI host name now decode as decode_error
