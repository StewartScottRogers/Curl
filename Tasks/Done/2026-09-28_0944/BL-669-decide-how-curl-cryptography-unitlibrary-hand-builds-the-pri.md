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
completed: 2026-09-28
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

- [x] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", with a table: primitive, specification, consumer, BCL availability per platform, the task that builds it, and the test-vector source.
- [x] The Decision fixes the API shape (span-based, one public type per primitive, names from the specifications), the namespace, the constant-time and zeroing rules, and states that nothing is omitted because the BCL lacks it.
- [x] For any primitive the ADR adds beyond BL-671 to BL-677, a task is filed on the board through `task-board.ps1 new` and named in the ADR.
- [x] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

- ADR-0118 (`ADR-0118-curl-cryptography-hand-builds-every-primitive-the-bcl-lacks-on-a-ci-platform.md`); 0118 was unused in this tree and in every branch (`git log --all`) on 2026-09-28.
- Rule decided: use the BCL only where it has the primitive on all three CI platforms; otherwise one hand-built implementation used everywhere (no per-platform fallback), for identical behaviour and so Windows-only lanes exercise the code macOS runs.
- BCL availability comes from Microsoft Learn "Cross-platform cryptography in .NET" (dated 2026-08-03, read 2026-09-28). Findings: `AesCcm` is gone on macOS in .NET 10; SHA-3, brainpool, ML-KEM, ML-DSA and FIPS 186-3 DSA are missing on macOS; `AesGcm` works on all three (16-byte tags only on Apple, which every consumer uses), so it is used directly and BL-565/BL-677 need no GCM fallback.
- OpenSSL 3.5.7 (Git for Windows) measured with `openssl list -tls-groups` and `openssl ciphers -s -v DEFAULT`: x448, brainpool tls13 groups, ffdhe, ML-KEM hybrids, DHE-RSA and ChaCha20 suites; CCM not in DEFAULT.
- Filed beyond BL-671 to BL-677: BL-737 (AES-CTR, AES-CBC-CTS), BL-738 (AES-CCM), BL-739 (constant-time finite-field DH), BL-740 (X448), BL-741 (Ed448), BL-742 (brainpool ECDH/ECDSA), BL-743 (SHA-3/SHAKE and ML-KEM), BL-744 (ML-DSA), BL-745 (DSA).
- Board links: BL-565, BL-681 and BL-686 now depend on BL-737 (CTR/CTS built once in `Curl.Cryptography`); BL-565 also on BL-668 because it now references that library. BL-686 got a Notes line saying so.
- Blowfish, CAST-128 and RC4 are recorded as not constant-time (key-dependent tables by design); the ADR says so rather than pretending otherwise.
- TLS cipher suites beyond OpenSSL's DEFAULT (Camellia, ARIA) are left to BL-695's ADR, which amends ADR-0118's list if it adopts them.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. ADR-0118 fixes how Curl.Cryptography hand-builds every primitive the BCL lacks on any CI platform; BL-737 to BL-745 filed
