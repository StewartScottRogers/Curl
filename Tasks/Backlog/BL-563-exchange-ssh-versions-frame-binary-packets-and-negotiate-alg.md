---
id: BL-563
title: Exchange SSH versions, frame binary packets and negotiate algorithms with KEXINIT
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-561, BL-528]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-563 — Exchange SSH versions, frame binary packets and negotiate algorithms with KEXINIT

## Goal

The SSH transport in `Curl.Protocol.Ssh.UnitLibrary` sends its identification string, reads the server's (skipping pre-banner lines), reads and writes unencrypted binary packets (RFC 4253 section 6: length, padding, payload, sequence numbers), sends a `KEXINIT` with BL-560's algorithm lists, and picks each algorithm by RFC 4253 section 7.1, failing as curl 8.21.0 fails when nothing matches.

## Context

- Conformance audit 2026-09-28, row 35 (Blocker, L+; SSH split: this framing, BL-564 key exchange, BL-565 cipher and MAC, BL-566 host key, BL-567/BL-568 user auth, BL-569 to BL-573 SFTP, BL-574/BL-577 SCP, BL-575 compression, BL-576 registration, BL-578 `-v`).
- Design: BL-560's ADR (identification string, algorithm lists, class structure). Contract: BL-561. Timeouts: BL-498's ADR.
- **BCL only.** Crypto comes from `System.Security.Cryptography`; this task needs only `RandomNumberGenerator` (padding and cookie), injected so tests are deterministic. If anything needed cannot be built on the BCL, move the task to `Blocked` for Stewart naming what is missing; never add a package.
- Measure with the reference curl against a local OpenSSH server through `Record-CurlExchange.ps1 -NoServer`: a server whose `KexAlgorithms` (then `Ciphers`, then `MACs`, then `HostKeyAlgorithms`) share nothing with Curl's lists, and a TCP server that sends a non-SSH banner (the HTTP mode of the script serves that); stderr and exit code.

## Acceptance criteria

- [ ] Measured first as above; stderr and exit code of each copied into Notes.
- [ ] `Curl.Protocol.Ssh.UnitTests` pin the identification string, packet framing (padding rules, block size, maximum length) against an in-memory peer, and the algorithm choice for matching and non-matching lists, with the measured exit code and message for each failure.
- [ ] No test needs `TestCategory=Integration`; tests are platform-neutral; the library references only `Curl.Protocol.Abstractions.UnitLibrary`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
