---
id: BL-680
title: Encrypt and authenticate SSH packets with the CBC, 3DES, Blowfish, CAST-128 and arcfour ciphers and the SHA-1, MD5 and RIPEMD-160 MACs
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-565, BL-674, BL-675, BL-676, BL-668]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-680 — Encrypt and authenticate SSH packets with the CBC, 3DES, Blowfish, CAST-128 and arcfour ciphers and the SHA-1, MD5 and RIPEMD-160 MACs

## Goal

The SSH packet layer also speaks every remaining cipher and MAC libssh2 1.11.1 offers: `aes256-cbc` (and `rijndael-cbc@lysator.liu.se`), `aes192-cbc`, `aes128-cbc`, `3des-cbc`, `blowfish-cbc`, `cast128-cbc`, `arcfour`, `arcfour128`, and `hmac-sha1`, `hmac-sha1-96`, `hmac-md5`, `hmac-md5-96`, `hmac-ripemd160` (and `hmac-ripemd160@openssh.com`), in the order and with the default-enabled set BL-560's ADR records.

## Context

- Conformance audit 2026-09-28, row 35; standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): whatever any official curl build offers, Curl offers. The list is libssh2 1.11.1's (https://libssh2.org/, checked 2026-09-28), the SSH library of curl.se's official Windows build of curl 8.22.0; BL-560's ADR confirms which of these the reference build actually puts in its `KEXINIT` by default.
- Builds on BL-565. Primitives: BCL `Aes` (CBC), `TripleDES`, `HMACSHA1`, `HMACMD5`; hand-built Blowfish BL-674, RIPEMD-160 BL-675, RC4 and CAST-128 BL-676. `arcfour128` discards the first 1536 keystream bytes (RFC 4345).
- Reference rule: BL-668 (add the `Curl.Cryptography.UnitLibrary` reference and amend `Curl.Protocol.Ssh.UnitLibrary/CLAUDE.md` if no earlier SSH task did).

## Acceptance criteria

- [ ] `Curl.Protocol.Ssh.UnitTests` round-trip packets for every listed cipher with a SHA-2 MAC and every listed MAC with `aes128-ctr` against the in-memory peer, with the truncated `-96` MACs checked on their first 12 bytes, and a corrupted MAC ending the session with BL-565's exit code.
- [ ] The offered `KEXINIT` cipher and MAC lists match BL-560's ADR byte for byte.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
