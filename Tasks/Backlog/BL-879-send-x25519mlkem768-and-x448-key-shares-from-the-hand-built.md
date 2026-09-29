---
id: BL-879
title: Send X25519MLKEM768 and x448 key shares from the hand-built TLS 1.3 client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-740, BL-743]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-879 — Send X25519MLKEM768 and x448 key shares from the hand-built TLS 1.3 client

## Goal

`Tls13ClientSettings` accepts `X25519MLKEM768` (`0x11ec`) and `x448` (`0x001e`) in `SupportedGroups` and `KeyShareGroups`, so the hand-built TLS 1.3 client can send `ClientHelloProfile.OpenSsl`'s groups and key shares (`0x11ec`, `0x001d`) and complete a handshake whichever of them the server picks.

## Context

- `ClientHelloProfile.OpenSsl` (BL-787, ADR-0140) offers groups `11ec 001d 0017 001e 0018 0019 0100 0101` and key shares for `11ec` and `001d`; today `TlsNamedGroup.CanShare` accepts neither `0x11ec` nor `0x001e`, so `Tls13ClientSettings.Validate` rejects the profile's lists. BL-820 (profile hellos in `HandBuiltTlsProvider`) waits on this.
- X25519MLKEM768 (draft-ietf-tls-ecdhe-mlkem): the client share is the ML-KEM-768 encapsulation key (1184 bytes) followed by the X25519 public key (32); the server share is the ML-KEM ciphertext (1088) followed by its X25519 key; the shared secret is the ML-KEM shared secret followed by the X25519 one. ML-KEM from BL-743, X448 from BL-740, both in `Curl.Cryptography.UnitLibrary`.
- Code: `Curl.Tls.UnitLibrary/TlsNamedGroup.cs`, `Tls13KeyShare.cs`, `X25519KeyShare.cs`.

## Acceptance criteria

- [ ] `Curl.Tls.UnitTests` complete a TLS 1.3 handshake against a test server that picks `X25519MLKEM768`, and one that picks `x448` (after a HelloRetryRequest when no x448 share was sent).
- [ ] A server share of the wrong length for either group fails with `illegal_parameter` as a typed `TlsHandshakeFailure`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-820.

## Log

- 2026-09-29: Created.
