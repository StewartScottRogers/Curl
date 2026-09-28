---
id: BL-565
title: Encrypt and authenticate SSH packets with the negotiated cipher and MAC
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-564]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-565 — Encrypt and authenticate SSH packets with the negotiated cipher and MAC

## Goal

After `NEWKEYS`, packets are encrypted and authenticated with the ciphers and MACs BL-560's ADR offers (`aes128-ctr`/`aes192-ctr`/`aes256-ctr`, `aes128-gcm@openssh.com`/`aes256-gcm@openssh.com`, `hmac-sha2-256`/`hmac-sha2-512` and their `-etm@openssh.com` forms, and whatever else the ADR lists), and a packet whose MAC or tag fails ends the session as curl 8.21.0 ends it.

## Context

- Conformance audit 2026-09-28, row 35. Builds on BL-564's keys.
- **BCL only.** `Aes` (CTR mode built on `EncryptEcb`), `AesGcm`, `HMACSHA256`, `HMACSHA512`, `CryptographicOperations.FixedTimeEquals` for MAC comparison. If something needed cannot be built on the BCL, move the task to `Blocked` for Stewart naming what is missing; never add a package.
- Test vectors: NIST SP 800-38A (CTR) and SP 800-38D (GCM) vectors for the primitives; round trips against the in-memory peer for the packet layer.

## Acceptance criteria

- [ ] `Curl.Protocol.Ssh.UnitTests` pin each cipher and MAC against published vectors, round-trip packets of several lengths for each negotiated pair, and show a corrupted MAC or tag ending the session with the exit code BL-560's ADR states.
- [ ] Encrypt-then-MAC and MAC-then-encrypt orderings are both covered.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
