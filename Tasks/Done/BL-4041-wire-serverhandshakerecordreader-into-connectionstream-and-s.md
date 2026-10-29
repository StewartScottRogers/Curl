---
id: BL-4041
title: Wire ServerHandshakeRecordReader into ConnectionStream and SslStreamTlsProvider.DescribeHandshake, with ADR (BL-3997 split)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4040]
touches: [Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitLibrary/ConnectionStream.cs, Curl.Networking.UnitTests/SslStreamTlsProviderTests.HandshakeEvent.cs, Documentation/Decisions]
requirement: none
created: 2026-10-28
completed: 2026-10-29
---
# BL-4041 — Wire ServerHandshakeRecordReader into ConnectionStream and SslStreamTlsProvider.DescribeHandshake, with ADR (BL-3997 split)

## Goal

On Linux, `-v` over the SslStream path prints `SSL connection using TLSv1.3 / TLS_AES_256_GCM_SHA384 / X25519MLKEM768 / RSASSA-PSS`, not `[blank] / UNDEF` (CGR-226, BL-3755).

## Context

`SslStreamTlsProvider.DescribeHandshake` never sets `TlsHandshakeEvent.NegotiatedGroupName` and `PeerSignatureTypeName`, which `OpenSslHandshakeText.ConnectionLine` prints. `ServerHandshakeRecordReader` (BL-4039 group, BL-4040 signature) extracts them from the plaintext handshake records; this task feeds it from `ConnectionStream.ReadAsync`.

## Acceptance criteria

- [x] `ConnectionStream.ReadAsync` shows every byte read to a `ServerHandshakeRecordReader`, set only when `!_matchesSchannelBuild`, and `SslStreamTlsProvider.DescribeHandshake` sets the group and signature names from it, for TLS 1.3 and TLS 1.2, pinned by unit tests in `SslStreamTlsProviderTests.HandshakeEvent.cs` (the test that pins them null is updated).
- [x] An ADR in `Documentation/Planning/Decisions` records where the names come from (Decided by Claude under Stewart's delegation), with its README row.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Split from BL-3997 (over its cost cap). BL-3997's design: the reader sees bytes beside `TicketRecords`; names follow OpenSSL's `SSL_get0_group_name` and `OBJ_nid2sn(SSL_get_peer_signature_type_nid)`.

- 2026-10-29 (lane 2): `ConnectionStream.HandshakeRecords` (a `ServerHandshakeRecordReader`, set by `SslStreamTlsProvider.OpenTransport` only in the OpenSSL build) sees every byte `ReadAsync` reads, beside `TicketRecords`; `DescribeHandshake` takes it and sets `NegotiatedGroupName` and `PeerSignatureTypeName`. The TLS 1.3 signature comes from the certificate's key algorithm OID through the new covered `KeyAlgorithmOf` (`X509Certificate.GetKeyAlgorithm`). Decision recorded as ADR-0989. Default taken: a failed handshake's event keeps no names, since the criterion names only the completed handshake.
- Tests: the default handshake test now pins a group, plus `RSASSA-PSS` under TLS 1.3 or either RSA type under TLS 1.2 (the server's TLS 1.2 scheme differs by platform, and macOS's SslStream may not reach TLS 1.3). A TLS 1.2 test pins both names, a Schannel-build test pins both null, and `KeyAlgorithmOf` is pinned for a certificate and for null. The TLS 1.2 test uses a host of its own: in the full class run Schannel resumed an earlier test's TLS 1.2 session, which has no ServerKeyExchange, so the group was null.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 0 failing members; `dotnet build` is clean; the fast tests all passed. The tests pin no paths, OS text or platform-only names, which is how the Linux and macOS CI criterion is met here.

## Log

- 2026-10-28: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 155a8ca0 fffcbc05 3d836d5e.
- 2026-10-29: Doing -> Done. -v over SslStream in the OpenSSL build names the TLS group and peer signature type from the plaintext handshake records
