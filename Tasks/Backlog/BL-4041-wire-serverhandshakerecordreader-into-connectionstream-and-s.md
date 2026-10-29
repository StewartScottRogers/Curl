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
completed:
---
# BL-4041 — Wire ServerHandshakeRecordReader into ConnectionStream and SslStreamTlsProvider.DescribeHandshake, with ADR (BL-3997 split)

## Goal

On Linux, `-v` over the SslStream path prints `SSL connection using TLSv1.3 / TLS_AES_256_GCM_SHA384 / X25519MLKEM768 / RSASSA-PSS`, not `[blank] / UNDEF` (CGR-226, BL-3755).

## Context

`SslStreamTlsProvider.DescribeHandshake` never sets `TlsHandshakeEvent.NegotiatedGroupName` and `PeerSignatureTypeName`, which `OpenSslHandshakeText.ConnectionLine` prints. `ServerHandshakeRecordReader` (BL-4039 group, BL-4040 signature) extracts them from the plaintext handshake records; this task feeds it from `ConnectionStream.ReadAsync`.

## Acceptance criteria

- [ ] `ConnectionStream.ReadAsync` shows every byte read to a `ServerHandshakeRecordReader`, set only when `!_matchesSchannelBuild`, and `SslStreamTlsProvider.DescribeHandshake` sets the group and signature names from it, for TLS 1.3 and TLS 1.2, pinned by unit tests in `SslStreamTlsProviderTests.HandshakeEvent.cs` (the test that pins them null is updated).
- [ ] An ADR in `Documentation/Planning/Decisions` records where the names come from (Decided by Claude under Stewart's delegation), with its README row.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

Split from BL-3997 (over its cost cap). BL-3997's design: the reader sees bytes beside `TicketRecords`; names follow OpenSSL's `SSL_get0_group_name` and `OBJ_nid2sn(SSL_get_peer_signature_type_nid)`.

## Log

- 2026-10-28: Created.
