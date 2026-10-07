---
id: BL-1111
title: Refuse HTTP/3 ENABLE_CONNECT_PROTOCOL and H3_DATAGRAM settings other than 0 or 1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Http3.UnitLibrary, Curl.Http3.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1111 — Refuse HTTP/3 ENABLE_CONNECT_PROTOCOL and H3_DATAGRAM settings other than 0 or 1

## Goal

A peer SETTINGS frame carrying `SETTINGS_ENABLE_CONNECT_PROTOCOL` (`0x08`) or `SETTINGS_H3_DATAGRAM` (`0x33`) with a value other than 0 or 1 is a connection error of type `H3_SETTINGS_ERROR`, as RFC 9220 / RFC 8441 and RFC 9297 require and nghttp3 (curl's HTTP/3 library) enforces; today both are accepted with any value.

## Context

- RFC 8441 section 3 (applied to HTTP/3 by RFC 9220 section 3, identifier `0x08`): "The value of the parameter MUST be 0 or 1." RFC 9297 section 2.1.1: `SETTINGS_H3_DATAGRAM` (`0x33`) takes 0 or 1, and "an endpoint that receives the H3_DATAGRAM setting with any other value MUST treat this as a connection error of type H3_SETTINGS_ERROR".
- nghttp3 `lib/nghttp3_conn.c` `nghttp3_conn_on_settings_entry_received` (https://github.com/ngtcp2/nghttp3): for `NGHTTP3_SETTINGS_ID_ENABLE_CONNECT_PROTOCOL` on a client, any value but 0 or 1 returns `NGHTTP3_ERR_H3_SETTINGS_ERROR`; for `NGHTTP3_SETTINGS_ID_H3_DATAGRAM`, the same.
- Curl today: `Curl.Http3.UnitLibrary/Http3SettingsFrame.cs` `ParsePayload` -> `ThrowIfForbidden` refuses the reserved HTTP/2 identifiers and duplicates only; `Http3SettingIdentifier.cs` names `0x01`, `0x06` and `0x07` only. `Http3FrameTests.Settings_RoundTrips` / the grease test already round-trip `(0x33, 1)`, which must keep working.
- Add the two identifiers to `Http3SettingIdentifier` with doc comments citing the RFCs, and refuse their out-of-range values in `ThrowIfForbidden` (or a sibling check) with `Http3ErrorCode.SettingsError`.

## Acceptance criteria

- [x] `Curl.Http3.UnitTests/Http3FrameTests.Settings_ForbiddenIdentifier_IsSettingsError` (or a new data-driven test beside it) gains cases `04 02 08 02` (ENABLE_CONNECT_PROTOCOL 2), `04 03 08 40 40` (64), `04 02 33 02` (H3_DATAGRAM 2) and an eight-byte-varint value, each `Http3ErrorCode.SettingsError`.
- [x] A test pins that the values 0 and 1 of both settings are read and kept in `Http3SettingsFrame.Settings`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Http3.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- `Http3SettingIdentifier` gains `EnableConnectProtocol` (0x08) and `H3Datagram` (0x33) and `IsZeroOrOneSetting`; `Http3SettingsFrame.ThrowIfForbidden` now also takes the value and refuses any but 0 or 1 for those two with `SettingsError`, as nghttp3 does. The check runs before the duplicate check, so either violation is the same error.
- The eight-byte-varint case is `c0 00 00 00 00 00 01 00` (256): an eight-byte encoding of 1 is legal and kept.
- Coverage: 100% line, 100% branch, 0 failing members; fast tests all green.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. HTTP/3 SETTINGS_ENABLE_CONNECT_PROTOCOL and SETTINGS_H3_DATAGRAM values other than 0 or 1 are H3_SETTINGS_ERROR
