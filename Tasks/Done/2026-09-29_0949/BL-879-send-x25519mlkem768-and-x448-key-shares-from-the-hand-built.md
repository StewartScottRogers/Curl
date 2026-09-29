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
completed: 2026-09-29
---
# BL-879 — Send X25519MLKEM768 and x448 key shares from the hand-built TLS 1.3 client

## Goal

`Tls13ClientSettings` accepts `X25519MLKEM768` (`0x11ec`) and `x448` (`0x001e`) in `SupportedGroups` and `KeyShareGroups`, so the hand-built TLS 1.3 client can send `ClientHelloProfile.OpenSsl`'s groups and key shares (`0x11ec`, `0x001d`) and complete a handshake whichever of them the server picks.

## Context

- `ClientHelloProfile.OpenSsl` (BL-787, ADR-0140) offers groups `11ec 001d 0017 001e 0018 0019 0100 0101` and key shares for `11ec` and `001d`; today `TlsNamedGroup.CanShare` accepts neither `0x11ec` nor `0x001e`, so `Tls13ClientSettings.Validate` rejects the profile's lists. BL-820 (profile hellos in `HandBuiltTlsProvider`) waits on this.
- X25519MLKEM768 (draft-ietf-tls-ecdhe-mlkem): the client share is the ML-KEM-768 encapsulation key (1184 bytes) followed by the X25519 public key (32); the server share is the ML-KEM ciphertext (1088) followed by its X25519 key; the shared secret is the ML-KEM shared secret followed by the X25519 one. ML-KEM from BL-743, X448 from BL-740, both in `Curl.Cryptography.UnitLibrary`.
- Code: `Curl.Tls.UnitLibrary/TlsNamedGroup.cs`, `Tls13KeyShare.cs`, `X25519KeyShare.cs`.

## Acceptance criteria

- [x] `Curl.Tls.UnitTests` complete a TLS 1.3 handshake against a test server that picks `X25519MLKEM768`, and one that picks `x448` (after a HelloRetryRequest when no x448 share was sent).
- [x] A server share of the wrong length for either group fails with `illegal_parameter` as a typed `TlsHandshakeFailure`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-820.
- Plan (delivered in-session: one library and its tests). `TlsNamedGroup` gains `X448` (0x001e) and `X25519MlKem768` (0x11ec), and `CanShare` accepts both. New `X448KeyShare` mirrors `X25519KeyShare`. New `X25519MlKem768KeyShare` owns an ML-KEM-768 key and an X25519 scalar, sends ek (1184) || x25519 (32), and turns a ciphertext (1088) || x25519 (32) server share into mlkem_ss || x25519_ss. `SystemTlsRandomSource.CreateKeyShare` makes both.
- Wrong length: both shares return null for a server share that is not exactly 56 / 1120 bytes, which `Tls13ClientHandshake.ReceiveServerShare` already turns into `illegal_parameter`. A degenerate X25519 half is refused the same way, and the ML-KEM half of the secret is zeroed first. ML-KEM decapsulation itself never fails (implicit rejection), so a tampered ciphertext surfaces later as the Finished check's `decrypt_error`.
- Choice: the server side of the hybrid (encapsulation) lives in the test project as `X25519MlKem768ServerShare`, not in the library, because the client never encapsulates. `Tls13TestServer` uses it when its `Group` is X25519MLKEM768, and now exposes `SentHelloRetryRequest`.
- Choice: `SystemTlsRandomSource.CreateKeyShare` now routes the finite-field range to `FfdheKeyShare` and everything else to `EcdhKeyShare.Generate` (which still throws `ArgumentOutOfRangeException` for a group it cannot share), keeping the method at cyclomatic complexity 10.
- TLS 1.2 is unaffected: `Tls12ClientSettings` still admits only X25519 and the NIST curves for ECDHE.
- No ADR: this implements the profile groups ADR-0140 and BL-787 already decided; there is no new behaviour decision.
- Verified 2026-09-29: `dotnet build Curl.slnx -warnaserror` clean; fast tests green; `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` shows 100% line, 100% branch, 0 failing members out of 865.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. The hand-built TLS 1.3 client shares and completes handshakes on X25519MLKEM768 and x448, so ClientHelloProfile.OpenSsl's groups validate
