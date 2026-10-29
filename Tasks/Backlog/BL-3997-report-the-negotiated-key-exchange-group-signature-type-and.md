---
id: BL-3997
title: Report the negotiated key-exchange group and peer signature type from the SslStream TLS path in the OpenSSL build (CGR-226 source)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitLibrary/ConnectionStream.cs, Curl.Networking.UnitLibrary/ServerHandshakeRecordReader.cs, Curl.Networking.UnitTests/ServerHandshakeRecordReaderTests.cs, Curl.Networking.UnitTests/SslStreamTlsProviderTests.HandshakeEvent.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3997 — Report the negotiated key-exchange group and peer signature type from the SslStream TLS path in the OpenSSL build (CGR-226 source)

## Goal

On Linux, `-v` over the SslStream path prints `SSL connection using TLSv1.3 / TLS_AES_256_GCM_SHA384 / X25519MLKEM768 / RSASSA-PSS`, not `[blank] / UNDEF`, as curl 8.18.0 OpenSSL does (CGR-226, BL-3755).

## Context

`OpenSslHandshakeText.ConnectionLine` already prints `TlsHandshakeEvent.NegotiatedGroupName` and `PeerSignatureTypeName`, but `SslStreamTlsProvider.DescribeHandshake` never sets them (the test pins them null), so Linux prints `[blank] / UNDEF`. SslStream does not expose them, but the plaintext handshake records cross `ConnectionStream` under it. The handshake message trace this task first also asked for is BL-4006 and BL-4007: SslStream decrypts the TLS 1.3 messages after ServerHello out of sight, so no honest trace can come from this path.

## Acceptance criteria

- [ ] Group and signature names are reported for a handshake on the SslStream path in the OpenSSL build (TLS 1.3 and TLS 1.2), pinned by unit tests.
- [ ] An ADR records where the names come from.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

Filed by BL-3755, which depends on this.

Design worked out by the 2026-10-28 claim (lane 3), before any edit; record it as an ADR (Decided by Claude under Stewart's delegation):
- New `ServerHandshakeRecordReader` (pure, unit-testable): `ConnectionStream.ReadAsync` shows it every byte read (beside `TicketRecords`), set only when `!_matchesSchannelBuild`. It joins the payloads of plaintext handshake records (content type 22) and stops at the first record of type 20 (ChangeCipherSpec) or 23, or at 64 KiB. TLS 1.3: ServerHello (or HelloRetryRequest, whose `selected_group` is the final group) then CCS or an encrypted record; TLS 1.2: ServerHello, Certificate, ServerKeyExchange, ServerHelloDone, then CCS.
- Group: TLS 1.3 from ServerHello's `key_share` extension (0x0033, first 2 bytes); TLS 1.2 from ServerKeyExchange when `curve_type` is 3 (named_curve). Names as OpenSSL's `SSL_get0_group_name`: canonical names from `OpenSslGroupList.cs`'s table (`secp256r1`, `x25519`, `X25519MLKEM768`, `ffdhe2048`, ...); `TlsNamedGroup` constants hold the IDs.
- Signature (`OBJ_nid2sn(SSL_get_peer_signature_type_nid)`): TLS 1.2 from the SKE's signature scheme (0x0804-0x0806/0x0809-0x080b `RSASSA-PSS`, 0x0201/0x0401/0x0501/0x0601 `rsaEncryption`, 0x0203/0x0403/0x0503/0x0603 and 0x081a-0x081c `id-ecPublicKey`, 0x0807 `ED25519`, 0x0808 `ED448`); TLS 1.3's CertificateVerify is encrypted, so from the server certificate's key, which TLS 1.3 fixes: RSA key `RSASSA-PSS` (PKCS#1 v1.5 is forbidden in 1.3), EC `id-ecPublicKey`, Ed25519 `ED25519`, Ed448 `ED448`. Measure real curl's names with `Record-CurlExchange.ps1` on Linux if available, else keep these OpenSSL short names (`id-ecPublicKey` is what curl prints for an ECDSA server).
- Wire in `SslStreamTlsProvider.DescribeHandshake` (pass the transport's reader); update the test that pins them null.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-28: Doing -> Backlog. Split before any edit: SslStream cannot expose the encrypted TLS 1.3 handshake messages, so the trace criterion moves to BL-4006 and BL-4007; criteria and touches narrowed to the group and signature names
