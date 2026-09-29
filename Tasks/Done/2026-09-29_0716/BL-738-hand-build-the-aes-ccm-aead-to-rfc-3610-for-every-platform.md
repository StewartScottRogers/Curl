---
id: BL-738
title: Hand-build the AES-CCM AEAD to RFC 3610 for every platform
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-670]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-738 — Hand-build the AES-CCM AEAD to RFC 3610 for every platform

## Goal

`Curl.Cryptography.UnitLibrary` seals and opens AES-CCM (RFC 3610, NIST SP 800-38C) with 16- and 8-byte tags on the BCL AES block cipher, so the TLS suites `TLS_AES_128_CCM_SHA256`, `TLS_AES_128_CCM_8_SHA256` and the TLS 1.2 `*-AES*-CCM*` suites work the same on Windows, Linux and macOS.

## Context

- ADR-0118 (BL-669): the BCL's `AesCcm` is not supported on macOS from .NET 10 (Microsoft Learn, "Cross-platform cryptography in .NET", checked 2026-09-28: OpenSSL support on macOS was removed in .NET 10), so AES-CCM is hand-built here and used on every platform. The type is `AeadAesCcm` so it never collides with `System.Security.Cryptography.AesCcm`.
- Consumers: the hand-built TLS client's record protection (BL-700 for TLS 1.3, BL-702 for TLS 1.2), reached through `--tls13-ciphers` and `--ciphers` (BL-709). OpenSSL's `DEFAULT` list does not enable CCM (`openssl ciphers -s -v DEFAULT`, OpenSSL 3.5.7, 2026-09-28), so it is used only when named.
- Vectors: RFC 3610 section 8 (packet vectors 1 to 24), NIST SP 800-38C appendix C (examples C.1 to C.3).

## Acceptance criteria

- [x] `Curl.Cryptography.UnitTests` pass RFC 3610 packet vectors 1 to 24 and NIST SP 800-38C C.1 to C.3 in both directions.
- [x] A changed tag or ciphertext makes `TryDecrypt` return `false`, compares the tag with `CryptographicOperations.FixedTimeEquals`, and leaves the plaintext buffer all zero.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- `AeadAesCcm` follows the BCL's `AesCcm` shape so a caller can swap one for the other:
  constructor takes the key (16, 24 or 32 bytes), the tag length is the `tag` span's
  length (an even 4 to 16), the nonce is 7 to 13 bytes. One type covers CCM (16-byte tag)
  and CCM_8 (8-byte tag) and every length RFC 3610's and NIST's vectors use (4, 6, 8, 10
  and 16). A payload longer than the nonce's length field allows throws
  `ArgumentException` (param `source`). No ADR: the API choice follows the precedent of
  `AeadAriaGcm` and ADR-0118.
- RFC 3610's 24 vectors were extracted from the RFC text (rfc-editor.org) by a
  PowerShell parse; NIST C.1 to C.3 and the extra pins (associated data at 0xFEFF and
  0xFF00 bytes, the long-length mark, and an empty payload) were cross-checked against
  the BCL's `AesCcm` on Windows before being pinned. The tests themselves use only
  `AeadAesCcm`, so they run on every platform.
- On a wrong tag `TryDecrypt` has already written the CTR-decrypted plaintext (CCM's MAC
  is over the plaintext), then zeroes it before returning `false`.
- Second run (2026-09-29): the first run's integration failure did not reproduce. The
  work was cherry-picked from `factory/BL-738-lane-4-20260929-023709` onto the current
  lane, and `dotnet build Curl.slnx -warnaserror` and every fast test project passed
  (Curl.Cryptography.UnitTests 1054 passed). `Measure-CodeQuality.ps1 -Library
  Curl.Cryptography.UnitLibrary`: 100% line, 100% branch, 538 members, 0 failing, worst
  CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Lane 4 could not integrate: fast tests failed after rebasing onto the other lanes' work. The work is on branch factory/BL-738-lane-4-20260929-023709; start with git cherry-pick --no-commit factory/BL-738-lane-4-20260929-023709 and fix it.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. AeadAesCcm seals and opens AES-CCM (RFC 3610, SP 800-38C) on every platform, pinned to all 27 published vectors
