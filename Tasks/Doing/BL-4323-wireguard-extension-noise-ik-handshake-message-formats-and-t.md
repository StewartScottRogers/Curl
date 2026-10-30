---
id: BL-4323
title: WireGuard extension: Noise IK handshake, message formats and transport data
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4325, BL-4326]
touches: [Curl.Extension.WireGuard.UnitLibrary/Noise, Curl.Extension.WireGuard.UnitLibrary/Messages, Curl.Extension.WireGuard.UnitTests/Noise, Curl.Extension.WireGuard.UnitTests/Messages]
requirement: none
created: 2026-10-29
completed:
---
# BL-4323 — WireGuard extension: Noise IK handshake, message formats and transport data

## Goal

`Curl.Extension.WireGuard.UnitLibrary` performs the WireGuard initiator's Noise_IKpsk2_25519_ChaChaPoly_BLAKE2s handshake and seals and opens transport data messages, given the configuration from BL-4326 and a datagram channel it is handed.

## Context

Split from BL-4147. Specification: WireGuard whitepaper (Donenfeld, revision e2da747, 2020-06-01) sections 5.4.1-5.4.7 and 6; Noise Protocol Framework revision 34 (IK pattern, psk2 modifier). Uses `X25519`, `AeadChaCha20Poly1305` and, from BL-4325, `Blake2s`, `HmacBlake2s`, `AeadXChaCha20Poly1305` in `Curl.Cryptography.UnitLibrary`. Inject `TimeProvider` (TAI64N timestamps, REKEY timers) and the random source; no network: tests drive the initiator against a responder written in the tests from the same sections, or against recorded bytes.

## Acceptance criteria

- [ ] `Messages/` encodes and decodes handshake initiation (type 1, 148 bytes), handshake response (type 2, 92 bytes), cookie reply (type 3, 64 bytes) and transport data (type 4) exactly as whitepaper 5.4.2-5.4.6 lays them out, including mac1 and mac2; unit tests.
- [ ] `Noise/` completes a handshake with a test responder implementing whitepaper 5.4.2-5.4.4, derives the same transport keys on both sides (5.4.5), and rejects a response with a bad mac1, wrong static key or replayed timestamp; unit tests.
- [ ] Transport data uses a 64-bit counter nonce, rejects replayed and out-of-window counters (whitepaper 5.4.6 and its referenced RFC 6479 sliding window), pads plaintext to 16 bytes, and a cookie reply makes the next initiation carry mac2 (5.4.7); unit tests.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1 -Library Curl.Extension.WireGuard.UnitLibrary`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 5fa32082 944a49cc efff73f2 bef89300 3d836d5e.
