---
id: BL-679
title: Encrypt SSH packets with chacha20-poly1305@openssh.com
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-565, BL-673, BL-668]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Documentation/Planning/Decisions/ADR-0259-chacha20-poly1305-packets-pad-to-eight-without-the-length-and-a-failed-tag-is-minus-12.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-679 — Encrypt SSH packets with chacha20-poly1305@openssh.com

## Goal

When `chacha20-poly1305@openssh.com` is negotiated, SSH packets are encrypted, their length field protected and their Poly1305 tag checked exactly as OpenSSH's `PROTOCOL.chacha20poly1305` specifies, using `Curl.Cryptography.UnitLibrary`'s raw ChaCha20 and Poly1305.

## Context

- Conformance audit 2026-09-28, row 35; standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-565 (packet layer after `NEWKEYS`); primitives BL-673. Offered position: BL-560's ADR.
- The construction: 64-byte key split into K_1 (length) and K_2 (payload); per-packet nonce is the sequence number; Poly1305 key from ChaCha20 block 0 under K_2; payload from block 1; no separate MAC is negotiated.
- Reference rule: BL-668 (add the `Curl.Cryptography.UnitLibrary` reference and amend `Curl.Protocol.Ssh.UnitLibrary/CLAUDE.md` if no earlier SSH task did).

## Acceptance criteria

- [x] `Curl.Protocol.Ssh.UnitTests` pin the ciphertext and tag of a packet for fixed keys and sequence number (computed independently in the test from the primitives), round-trip packets of several lengths against the in-memory peer, and end the session on a corrupted length or tag with the exit code BL-565 uses.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- `PacketProtection/ChaCha20Poly1305PacketProtection.cs` implements OpenSSH's construction over `Curl.Cryptography`'s `ChaCha20` (8-byte nonce, 64-bit counter) and `Poly1305`; `SshPacketProtections` registers it with a 64-byte key and no IV, so `SshAlgorithmCatalogue.Implemented` now offers it first in both presets. The `Curl.Cryptography.UnitLibrary` reference was already in place (BL-668); `CLAUDE.md` now names `ChaCha20` and `Poly1305`.
- Decisions (ADR-0259, decided by Claude under Stewart's delegation): block size 8 with the length field outside the padding, as OpenSSH frames AEAD packets; a failed tag is libssh2's -12, as for AES-GCM (from libssh2's `decrypt()`, not measured - BL-897 measures the AEAD codes on the OpenSSL build); a corrupted length that no longer frames is ADR-0206's -8.
- Tests: `ChaCha20Poly1305PacketProtectionTests` pins ciphertext and tag against an independent build from the primitives; `SshPacketProtectionsTests` round-trips 9 payload lengths; `SshTransportTests.KeyExchange` ends a re-exchange on a corrupted tag (-12) and length (-8), exit 2; `SshProtocolHandlerTests.ExecuteAsync_ServerOffersOnlyChaCha20Poly1305_TransfersOverIt` downloads 70000 bytes end to end from `InMemorySshServer`, which gained a `Cipher` property. Existing tests that expected AES-GCM or AES-CTR against an OpenSSH server now expect ChaCha20-Poly1305.
- Touches widened to the new ADR and `Documentation/Planning/Decisions/README.md` (its index); no task in Doing names them.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. SSH packets are sealed and opened with chacha20-poly1305@openssh.com, now both presets' first agreed cipher against OpenSSH
