---
id: BL-4040
title: ServerHandshakeRecordReader reports the peer signature type name for TLS 1.2 and TLS 1.3 (BL-3997 split)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4039]
touches: [Curl.Networking.UnitLibrary/ServerHandshakeRecordReader.cs, Curl.Networking.UnitTests/ServerHandshakeRecordReaderTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4040 — ServerHandshakeRecordReader reports the peer signature type name for TLS 1.2 and TLS 1.3 (BL-3997 split)

## Goal

`ServerHandshakeRecordReader` also reports the peer signature type name as OpenSSL's `OBJ_nid2sn(SSL_get_peer_signature_type_nid)` prints it.

## Context

Part of CGR-226 (BL-3755, BL-3997). TLS 1.3's CertificateVerify is encrypted, so its signature type is derived from the server certificate's key; TLS 1.2's comes from ServerKeyExchange.

## Acceptance criteria

- [ ] TLS 1.2: the SKE signature scheme maps to a name: 0x0804-0x0806 and 0x0809-0x080b `RSASSA-PSS`; 0x0201/0x0401/0x0501/0x0601 `rsaEncryption`; 0x0203/0x0403/0x0503/0x0603 and 0x081a-0x081c `id-ecPublicKey`; 0x0807 `ED25519`; 0x0808 `ED448`; each pinned by a unit test in `ServerHandshakeRecordReaderTests`.
- [ ] TLS 1.3: the name comes from the server certificate's key algorithm, passed to the reader's name method by the caller (the Certificate message is encrypted, so the caller takes it from `SslStream.RemoteCertificate`): RSA `RSASSA-PSS`, EC `id-ecPublicKey`, Ed25519 `ED25519`, Ed448 `ED448`, pinned by unit tests; an unknown scheme or malformed input yields no name and never throws.
- [ ] `dotnet build` is clean and the fast tests pass; coverage of the class stays at 100% line and branch.

## Notes

Split from BL-3997 (over its cost cap). BL-4041 passes the certificate key algorithm. Real curl's names: measure with `Record-CurlExchange.ps1` on Linux if available, else keep these OpenSSL short names.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 88a180fe b62fdaeb da2ed308.
