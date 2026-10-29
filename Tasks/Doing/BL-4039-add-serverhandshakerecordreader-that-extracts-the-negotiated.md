---
id: BL-4039
title: Add ServerHandshakeRecordReader that extracts the negotiated key-exchange group name from plaintext TLS handshake records (BL-3997 split)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerHandshakeRecordReader.cs, Curl.Networking.UnitTests/ServerHandshakeRecordReaderTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4039 — Add ServerHandshakeRecordReader that extracts the negotiated key-exchange group name from plaintext TLS handshake records (BL-3997 split)

## Goal

A pure `ServerHandshakeRecordReader` turns the server-to-client bytes of a TLS handshake into the negotiated key-exchange group name, as OpenSSL's `SSL_get0_group_name` names it.

## Context

Part of CGR-226 (BL-3755, BL-3997). SslStream hides the group, but the plaintext handshake records cross `ConnectionStream` under it. Canonical names come from `OpenSslGroupList.cs`'s table; `TlsNamedGroup` holds the IDs.

## Acceptance criteria

- [ ] `ServerHandshakeRecordReader` accepts bytes in any chunking, joins the payloads of plaintext handshake records (content type 22), and stops at the first record of type 20 or 23 or at 64 KiB, pinned by `ServerHandshakeRecordReaderTests`.
- [ ] TLS 1.3: the group is read from ServerHello's `key_share` extension (0x0033, first 2 bytes), or HelloRetryRequest's `selected_group`; TLS 1.2: from ServerKeyExchange when `curve_type` is 3 (named_curve); names such as `secp256r1`, `x25519`, `X25519MLKEM768`, `ffdhe2048` are pinned by unit tests; truncated or malformed input yields no name and never throws.
- [ ] `dotnet build` is clean and the fast tests pass; the new class meets the 100% line and branch coverage gate.

## Notes

Split from BL-3997 (over its cost cap). The signature type is BL-4040; wiring and the ADR are BL-4041.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 187274bf 18ff86a5 93da3646.
