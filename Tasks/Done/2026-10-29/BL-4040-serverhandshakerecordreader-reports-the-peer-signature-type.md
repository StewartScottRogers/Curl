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
completed: 2026-10-28
---
# BL-4040 — ServerHandshakeRecordReader reports the peer signature type name for TLS 1.2 and TLS 1.3 (BL-3997 split)

## Goal

`ServerHandshakeRecordReader` also reports the peer signature type name as OpenSSL's `OBJ_nid2sn(SSL_get_peer_signature_type_nid)` prints it.

## Context

Part of CGR-226 (BL-3755, BL-3997). TLS 1.3's CertificateVerify is encrypted, so its signature type is derived from the server certificate's key; TLS 1.2's comes from ServerKeyExchange.

## Acceptance criteria

- [x] TLS 1.2: the SKE signature scheme maps to a name: 0x0804-0x0806 and 0x0809-0x080b `RSASSA-PSS`; 0x0201/0x0401/0x0501/0x0601 `rsaEncryption`; 0x0203/0x0403/0x0503/0x0603 and 0x081a-0x081c `id-ecPublicKey`; 0x0807 `ED25519`; 0x0808 `ED448`; each pinned by a unit test in `ServerHandshakeRecordReaderTests`.
- [x] TLS 1.3: the name comes from the server certificate's key algorithm, passed to the reader's name method by the caller (the Certificate message is encrypted, so the caller takes it from `SslStream.RemoteCertificate`): RSA `RSASSA-PSS`, EC `id-ecPublicKey`, Ed25519 `ED25519`, Ed448 `ED448`, pinned by unit tests; an unknown scheme or malformed input yields no name and never throws.
- [x] `dotnet build` is clean and the fast tests pass; coverage of the class stays at 100% line and branch.

## Notes

Split from BL-3997 (over its cost cap). BL-4041 passes the certificate key algorithm. Real curl's names: measure with `Record-CurlExchange.ps1` on Linux if available, else keep these OpenSSL short names.

- 2026-10-28 (lane 8): No Linux curl was available on this Windows lane, so the names are OpenSSL's short names as listed (the same table `OpenSslHandshakeNames` uses for the hand-built path, ADR-0983). The API is `ServerHandshakeRecordReader.PeerSignatureTypeName(string? certificateKeyAlgorithmOid)`. The handshake counts as TLS 1.3 when the ServerHello's `supported_versions` is 0x0304, and then the name comes from the key OID: RSA (1.2.840.113549.1.1.1) and RSA-PSS (…1.10) give `RSASSA-PSS`, because TLS 1.3 signs only with PSS for RSA keys. Any other handshake takes the last ServerKeyExchange's scheme. Without the cipher suite, the reader cannot tell ECDHE from DHE parameters up front. Decision: it tries ECDHE (curve_type 3), then DHE (p, g, Ys), and accepts a reading only when the scheme and signature end the message exactly. That way a DHE prime 0x03xx bytes long is never misread as ECDHE. A TLS 1.2 RSA key exchange has no ServerKeyExchange, so it gives no name, which matches OpenSSL reporting no peer signature type. BL-4041 wires the reader in and records the source ADR.
- Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary: first run flagged `PeerSignatureTypeName` at complexity 16. After splitting out the TLS 1.2 and TLS 1.3 lookups: 0 failing members, coverage 100%. Full fast tests green on Windows. The tests are pure byte parsing with no paths or OS text, so they are platform-neutral.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 88a180fe b62fdaeb da2ed308.
- 2026-10-28: Doing -> Done. ServerHandshakeRecordReader.PeerSignatureTypeName names the server's signature type from TLS 1.2 ServerKeyExchange schemes and the TLS 1.3 certificate key
