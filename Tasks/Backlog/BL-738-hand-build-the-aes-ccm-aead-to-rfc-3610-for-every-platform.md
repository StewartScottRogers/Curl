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
completed:
---
# BL-738 — Hand-build the AES-CCM AEAD to RFC 3610 for every platform

## Goal

`Curl.Cryptography.UnitLibrary` seals and opens AES-CCM (RFC 3610, NIST SP 800-38C) with 16- and 8-byte tags on the BCL AES block cipher, so the TLS suites `TLS_AES_128_CCM_SHA256`, `TLS_AES_128_CCM_8_SHA256` and the TLS 1.2 `*-AES*-CCM*` suites work the same on Windows, Linux and macOS.

## Context

- ADR-0118 (BL-669): the BCL's `AesCcm` is not supported on macOS from .NET 10 (Microsoft Learn, "Cross-platform cryptography in .NET", checked 2026-09-28: OpenSSL support on macOS was removed in .NET 10), so AES-CCM is hand-built here and used on every platform. The type is `AeadAesCcm` so it never collides with `System.Security.Cryptography.AesCcm`.
- Consumers: the hand-built TLS client's record protection (BL-700 for TLS 1.3, BL-702 for TLS 1.2), reached through `--tls13-ciphers` and `--ciphers` (BL-709). OpenSSL's `DEFAULT` list does not enable CCM (`openssl ciphers -s -v DEFAULT`, OpenSSL 3.5.7, 2026-09-28), so it is used only when named.
- Vectors: RFC 3610 section 8 (packet vectors 1 to 24), NIST SP 800-38C appendix C (examples C.1 to C.3).

## Acceptance criteria

- [ ] `Curl.Cryptography.UnitTests` pass RFC 3610 packet vectors 1 to 24 and NIST SP 800-38C C.1 to C.3 in both directions.
- [ ] A changed tag or ciphertext makes `TryDecrypt` return `false`, compares the tag with `CryptographicOperations.FixedTimeEquals`, and leaves the plaintext buffer all zero.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Lane 4 could not integrate: fast tests failed after rebasing onto the other lanes' work. The work is on branch factory/BL-738-lane-4-20260929-023709; start with git cherry-pick --no-commit factory/BL-738-lane-4-20260929-023709 and fix it.
