---
id: BL-679
title: Encrypt SSH packets with chacha20-poly1305@openssh.com
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-565, BL-673, BL-668]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-679 — Encrypt SSH packets with chacha20-poly1305@openssh.com

## Goal

When `chacha20-poly1305@openssh.com` is negotiated, SSH packets are encrypted, their length field protected and their Poly1305 tag checked exactly as OpenSSH's `PROTOCOL.chacha20poly1305` specifies, using `Curl.Cryptography.UnitLibrary`'s raw ChaCha20 and Poly1305.

## Context

- Conformance audit 2026-09-28, row 35; standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-565 (packet layer after `NEWKEYS`); primitives BL-673. Offered position: BL-560's ADR.
- The construction: 64-byte key split into K_1 (length) and K_2 (payload); per-packet nonce is the sequence number; Poly1305 key from ChaCha20 block 0 under K_2; payload from block 1; no separate MAC is negotiated.
- Reference rule: BL-668 (add the `Curl.Cryptography.UnitLibrary` reference and amend `Curl.Protocol.Ssh.UnitLibrary/CLAUDE.md` if no earlier SSH task did).

## Acceptance criteria

- [ ] `Curl.Protocol.Ssh.UnitTests` pin the ciphertext and tag of a packet for fixed keys and sequence number (computed independently in the test from the primitives), round-trip packets of several lengths against the in-memory peer, and end the session on a corrupted length or tag with the exit code BL-565 uses.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
