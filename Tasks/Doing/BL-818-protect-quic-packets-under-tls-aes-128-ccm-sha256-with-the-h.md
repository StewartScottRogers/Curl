---
id: BL-818
title: Protect QUIC packets under TLS_AES_128_CCM_SHA256 with the hand-built AES-CCM
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-723, BL-738]
touches: [Curl.Quic.UnitLibrary, Curl.Quic.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-818 — Protect QUIC packets under TLS_AES_128_CCM_SHA256 with the hand-built AES-CCM

## Goal

`QuicPacketProtection.CanProtect(0x1304)` is true and `QuicPacketProtection` protects and unprotects packets under `TLS_AES_128_CCM_SHA256` (`AEAD_AES_128_CCM`, 16-byte tag, AES-ECB header protection) with the hand-built AES-CCM of BL-738.

## Context

- BL-723 built QUIC packet protection for `0x1301`, `0x1302` and `0x1303` and refuses the CCM suites with an `ArgumentException` until an AES-CCM exists on every platform (the BCL's `AesCcm` is missing on macOS, ADR-0118).
- RFC 9001 section 5.3 allows `AEAD_AES_128_CCM`; `TLS_AES_128_CCM_8_SHA256` (8-byte tag) is not used with QUIC and stays refused.
- Start at `Curl.Quic.UnitLibrary/QuicPayloadProtection.cs` (the AEAD choice) and `QuicPacketProtection.CanProtect`; add an `IQuicPacketAead` over the BL-738 primitive.

## Acceptance criteria

- [ ] `Curl.Quic.UnitTests` round-trips an Initial-style long-header packet and a short-header packet under `Tls13CipherSuite.Aes128CcmSha256`, and a tampered one is dropped as `DroppedAuthenticationFailed`.
- [ ] `QuicPacketProtection.CanProtect(0x1304)` is true and `CanProtect(0x1305)` is still false.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Quic.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
