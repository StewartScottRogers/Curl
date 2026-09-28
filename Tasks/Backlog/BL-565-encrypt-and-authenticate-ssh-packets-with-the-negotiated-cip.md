---
id: BL-565
title: Encrypt and authenticate SSH packets with the negotiated cipher and MAC
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-564, BL-737, BL-668]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-565 — Encrypt and authenticate SSH packets with the negotiated cipher and MAC

## Goal

After `NEWKEYS`, packets are encrypted and authenticated with the ciphers and MACs BL-560's ADR offers (`aes128-ctr`/`aes192-ctr`/`aes256-ctr`, `aes128-gcm@openssh.com`/`aes256-gcm@openssh.com`, `hmac-sha2-256`/`hmac-sha2-512` and their `-etm@openssh.com` forms; `chacha20-poly1305@openssh.com` is BL-679 and the CBC, 3DES, Blowfish, CAST-128, arcfour, SHA-1, MD5 and RIPEMD-160 algorithms are BL-680), and a packet whose MAC or tag fails ends the session as curl 8.21.0 ends it.

## Context

- Conformance audit 2026-09-28, row 35. Builds on BL-564's keys.
- **BCL only.** `AesCtr` from `Curl.Cryptography.UnitLibrary` (BL-737, ADR-0118: CTR is built once there on the BCL's `Aes.EncryptEcb`, not in the SSH library), `AesGcm`, `HMACSHA256`, `HMACSHA512`, `CryptographicOperations.FixedTimeEquals` for MAC comparison. If `AesGcm` is unsupported on a CI platform, use the fallback BL-669's ADR names (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28: hand-built in `Curl.Cryptography.UnitLibrary`, never a package, never a task blocked for a missing primitive). Structure the packet layer so BL-679 and BL-680 add ciphers and MACs without reshaping it.
- Test vectors: NIST SP 800-38A (CTR) and SP 800-38D (GCM) vectors for the primitives; round trips against the in-memory peer for the packet layer.

## Acceptance criteria

- [ ] `Curl.Protocol.Ssh.UnitTests` pin each cipher and MAC against published vectors, round-trip packets of several lengths for each negotiated pair, and show a corrupted MAC or tag ending the session with the exit code BL-560's ADR states.
- [ ] Encrypt-then-MAC and MAC-then-encrypt orderings are both covered.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
