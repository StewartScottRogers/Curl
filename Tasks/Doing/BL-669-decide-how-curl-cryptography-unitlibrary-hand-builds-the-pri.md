---
id: BL-669
title: Decide how Curl.Cryptography.UnitLibrary hand-builds the primitives the BCL lacks
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-669 — Decide how Curl.Cryptography.UnitLibrary hand-builds the primitives the BCL lacks

## Goal

An ADR fixes how `Curl.Cryptography.UnitLibrary` provides every cryptographic primitive curl's SSH, TLS, QUIC, NTLM and Kerberos code needs that `System.Security.Cryptography` does not offer on all three CI platforms: the list, the public API shape, the constant-time rules, and the test vectors each must pass.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", Stewart 2026-09-28): QUIC and HTTP/3, SSH's Curve25519, Ed25519 and ChaCha20-Poly1305, Kerberos and the like are built by hand in their own library; never a package, never left out.
- Known needs, each with its consumer: X25519 (RFC 7748; SSH `curve25519-sha256`, TLS 1.3 and QUIC key shares) BL-671; Ed25519 (RFC 8032; `ssh-ed25519` host and user keys, TLS certificates) BL-672; raw ChaCha20 and Poly1305 plus the RFC 8439 AEAD (`chacha20-poly1305@openssh.com` needs the raw pieces, QUIC header protection needs raw ChaCha20, the BCL's `ChaCha20Poly1305` exposes neither and is not supported everywhere) BL-673; Blowfish and bcrypt-pbkdf (`blowfish-cbc`, encrypted `openssh-key-v1` keys) BL-674; MD4 (NTLM, Kerberos RC4 string-to-key) and RIPEMD-160 (`hmac-ripemd160`) BL-675; RC4 (`arcfour`, `arcfour128`, Kerberos `rc4-hmac`) and CAST-128 (`cast128-cbc`) BL-676; HPKE (RFC 9180, for ECH) BL-677. The SSH list is libssh2 1.11.1's (the SSH library of curl.se's official Windows build of curl 8.22.0, checked 2026-09-28 at https://curl.se/windows/ and https://libssh2.org/); BL-560's ADR may add more.
- Check on Windows, Linux and macOS (the CI matrix) whether `AesGcm.IsSupported`, `AesCcm.IsSupported`, `ChaCha20Poly1305.IsSupported`, `DSA` and `ECDiffieHellman` on each NIST curve are available; any gap is a further hand-built primitive in this ADR.
- Security: hand-written crypto must be constant-time where secrets are involved (no secret-dependent branches or table indexes; compare with `CryptographicOperations.FixedTimeEquals`) and zero its secrets (`CryptographicOperations.ZeroMemory`).
- The library references only the BCL (BL-667's ADR lists who may reference it).

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", with a table: primitive, specification, consumer, BCL availability per platform, the task that builds it, and the test-vector source.
- [ ] The Decision fixes the API shape (span-based, one public type per primitive, names from the specifications), the namespace, the constant-time and zeroing rules, and states that nothing is omitted because the BCL lacks it.
- [ ] For any primitive the ADR adds beyond BL-671 to BL-677, a task is filed on the board through `task-board.ps1 new` and named in the ADR.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
