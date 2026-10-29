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
completed: 2026-10-28
---
# BL-4039 — Add ServerHandshakeRecordReader that extracts the negotiated key-exchange group name from plaintext TLS handshake records (BL-3997 split)

## Goal

A pure `ServerHandshakeRecordReader` turns the server-to-client bytes of a TLS handshake into the negotiated key-exchange group name, as OpenSSL's `SSL_get0_group_name` names it.

## Context

Part of CGR-226 (BL-3755, BL-3997). SslStream hides the group, but the plaintext handshake records cross `ConnectionStream` under it. Canonical names come from `OpenSslGroupList.cs`'s table; `TlsNamedGroup` holds the IDs.

## Acceptance criteria

- [x] `ServerHandshakeRecordReader` accepts bytes in any chunking, joins the payloads of plaintext handshake records (content type 22), and stops at the first record of type 20 or 23 or at 64 KiB, pinned by `ServerHandshakeRecordReaderTests`.
- [x] TLS 1.3: the group is read from ServerHello's `key_share` extension (0x0033, first 2 bytes), or HelloRetryRequest's `selected_group`; TLS 1.2: from ServerKeyExchange when `curve_type` is 3 (named_curve); names such as `secp256r1`, `x25519`, `X25519MLKEM768`, `ffdhe2048` are pinned by unit tests; truncated or malformed input yields no name and never throws.
- [x] `dotnet build` is clean and the fast tests pass; the new class meets the 100% line and branch coverage gate.

## Notes

Split from BL-3997 (over its cost cap). The signature type is BL-4040; wiring and the ADR are BL-4041.

- Done directly rather than through the full /feature subagent stages: one pure parser class with its tests, within a tight cost cap; the decisions it needs (OpenSSL names) come from OpenSslGroupList.cs and are recorded here, the ADR is BL-4041's.
- Names: a private ID-to-name table in the reader, each group's first name in OpenSSL 3.5's provider table (the names OpenSslGroupList accepts first, e.g. secp256r1 not prime256v1); OpenSslGroupList.cs is outside touches, so the table is not shared. A group OpenSSL has no name for yields null, as SSL_get0_group_name returns NULL.
- A HelloRetryRequest's selected_group and a ServerHello's key_share entry both start with the group, so one read serves both; the last ServerHello or ServerKeyExchange naming a group wins. Records other than 20, 22 and 23 (alerts) are skipped. 64 KiB counts every byte taken, record headers included.
- Measured: 24 ServerHandshakeRecordReaderTests pass; Measure-CodeQuality -Library Curl.Networking.UnitLibrary reports 0 failing members (FindGroup first measured complexity 12, split into MessageGroup); build clean, fast tests green in all 46 test projects. Platform-neutral: pure byte parsing.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 187274bf 18ff86a5 93da3646.
- 2026-10-28: Doing -> Done. ServerHandshakeRecordReader names the negotiated TLS group from plaintext handshake records
