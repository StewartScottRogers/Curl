---
id: BL-678
title: Run the curve25519-sha256 SSH key exchange and verify ssh-ed25519 host keys
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-564, BL-671, BL-672, BL-668]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-678 — Run the curve25519-sha256 SSH key exchange and verify ssh-ed25519 host keys

## Goal

The SSH transport offers and runs `curve25519-sha256` and `curve25519-sha256@libssh.org` (RFC 8731) in the position BL-560's ADR gives them, and verifies `ssh-ed25519` host-key signatures (RFC 8709) over the exchange hash, using `Curl.Cryptography.UnitLibrary`'s X25519 and Ed25519.

## Context

- Conformance audit 2026-09-28, row 35; standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): SSH's Curve25519 and Ed25519 are built by hand, never left out. Builds on BL-564 (key exchange framework, exchange hash, key derivation).
- Primitives: BL-671 (X25519), BL-672 (Ed25519). Reference rule: BL-668 lets `Curl.Protocol.Ssh.UnitLibrary` reference `Curl.Cryptography.UnitLibrary`; add that `ProjectReference` here if no earlier SSH task did, and amend `Curl.Protocol.Ssh.UnitLibrary/CLAUDE.md` to name it.
- RFC 8731: shared secret encoded as an mpint, all-zero secret rejected. RFC 8709: `ssh-ed25519` key and signature blob format.
- Test vector: fixed client ephemeral key and a test peer with a fixed Ed25519 host key; cross-check the exchange hash computed independently in the test from RFC 8731's definition.

## Acceptance criteria

- [x] `Curl.Protocol.Ssh.UnitTests` complete a `curve25519-sha256` exchange against the in-memory peer with an `ssh-ed25519` host key, pinning the exchange hash and derived keys for fixed inputs.
- [x] An all-zero shared secret, a bad Ed25519 signature and a malformed host-key blob each end the session with the exit code and message BL-564 recorded for a failed key exchange.
- [x] The `KEXINIT` lists pin the algorithm order BL-560's ADR states, with both names present.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- K is the 32 X25519 output bytes read as a big-endian unsigned integer and written as an mpint, exactly as RFC 8731 section 3 says (no byte reversal); an all-zero X25519 result (a low-order server key such as u = 0 or 1) and a server key that is not 32 bytes end the exchange with BL-564's measured `-8, Unable to exchange encryption keys`.
- The client's X25519 private key comes through a new `ISshEphemeralKeySource.CreateX25519PrivateKey`, so tests fix it; the production source uses `X25519.GeneratePrivateKey`, and the key is zeroed after use.
- Both `curve25519-sha256` and `curve25519-sha256@libssh.org` register in `SshKeyExchangeMethods`; the offered order is the preset's (ADR-0122, measured from the OpenSSL build: `curve25519-sha256,curve25519-sha256@libssh.org,ecdh-sha2-nistp256,...`), pinned by `SshKexInitTests.ForClient_TodaysCatalogue_...`. The Windows preset does not offer them, matching the WinCNG build. `ssh-ed25519` sits after the ECDSA keys, as the OpenSSL preset lists it.
- `ssh-ed25519` host key: a 32-byte key string (anything else, or a truncated blob, fails with -8); a signature that is not 64 bytes or does not verify fails with -8.
- Test vectors: client X25519 key is RFC 7748 section 6.1's Alice key, server key a fixed value; host key is RFC 8032 section 7.1 test 1. The test server computes H from RFC 8731's definition independently, and the pinned test checks both.
- `ExchangeKeysAsync_MethodOrHostKeyNotImplemented_ThrowsNotSupported` now uses `sntrup761x25519-sha512@openssh.com` and `ssh-ed25519-cert-v01@openssh.com`, still unimplemented.
- No ADR: no new decision beyond what RFC 8731, RFC 8709 and ADR-0122 already fix. `Curl.Protocol.Ssh.UnitLibrary/CLAUDE.md` names X25519 and Ed25519 as the Cryptography types it uses (the ProjectReference already existed).
- One full fast run saw a single Curl.Authentication.UnitTests failure that passed on three reruns alone and on a full rerun; that project is untouched here, so it is load-related flakiness, not this change.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. The SSH transport runs curve25519-sha256 (and its libssh.org name) and verifies ssh-ed25519 host keys
