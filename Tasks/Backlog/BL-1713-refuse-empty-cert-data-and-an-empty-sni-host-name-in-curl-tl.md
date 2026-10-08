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
completed:
---
# BL-1713 — Refuse empty cert_data and an empty SNI host name in Curl.Tls decoders

## Goal

`CertificateMessage.Decode` refuses a certificate entry with zero-length `cert_data`, and `ServerNameExtension.DecodeHostName` refuses a zero-length host name, each with `decode_error`.

## Context

- Found by BL-1523's adversarial tests. RFC 8446 section 4.4.2 gives `cert_data` the range `<1..2^24-1>`; RFC 6066 section 3 gives `HostName` the range `<1..2^16-1>`. Both decoders accept length 0 today.
- Reproductions: `CertificateMessage.Decode([0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00])` and `ServerNameExtension.DecodeHostName([0x00, 0x03, 0x00, 0x00, 0x00])` both succeed.
- Check the TLS 1.2 `Tls12CertificateMessage` decoder for the same gap (RFC 5246 `ASN.1Cert<1..2^24-1>`).

## Acceptance criteria

- [ ] Both reproductions above answer `TlsAlertDescription.DecodeError`, each pinned by a test in `Curl.Tls.UnitTests`.
- [ ] The TLS 1.2 certificate decoder refuses an empty certificate the same way, or Notes say why it already does.
- [ ] `dotnet build` is clean and the fast tests pass; Curl.Tls.UnitLibrary keeps 100% line and branch coverage.

## Notes

## Log

- 2026-10-07: Created.
