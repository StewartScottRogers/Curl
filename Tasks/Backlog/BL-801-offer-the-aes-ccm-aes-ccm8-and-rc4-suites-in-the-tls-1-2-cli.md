---
id: BL-801
title: Offer the AES-CCM, AES-CCM8 and RC4 suites in the TLS 1.2 client handshake
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-703, BL-796]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-801 — Offer the AES-CCM, AES-CCM8 and RC4 suites in the TLS 1.2 client handshake

## Goal

`Tls12CipherSuite` offers and negotiates the AES-CCM and AES-CCM8 suites of RFC 6655 and RFC 7251 and the RC4 suites, so `Tls12ClientHandshake` completes a handshake on any of them using the record-layer ciphers BL-796 adds.

## Context

- BL-703 built `Tls12ClientHandshake` in `Curl.Tls.UnitLibrary`: the I/O-free TLS 1.2/1.1/1.0 client handshake over the suite table `Tls12CipherSuite` (73 suites today), with `Tls12ClientSettings`, `Tls12Session` and the in-memory `Tls12TestServer` in `Curl.Tls.UnitTests`. Its decisions are in ADR-0154 (`Documentation/Planning/Decisions/ADR-0154-the-tls-1-2-client-handshake-refuses-legacy-renegotiation-ignores-hello-request-and-matches-openssls-alerts.md`).
- BL-796 adds AES-128/256-CCM, AES-128/256-CCM8 and RC4-128 to `Tls12BulkCipher` (the record layer). This task only wires the suites that use them into the handshake's suite table.
- ADR-0140 ("What the client supports") requires every suite of OpenSSL 3.5.5's `ALL:COMPLEMENTOFALL` list whose key exchange curl can drive, plus LibreSSL's RC4 and 3DES suites.
- Suites to add:
  - RFC 6655 (TLS 1.2 only, SHA-256 PRF): `0xc09c` RSA_WITH_AES_128_CCM, `0xc09d` RSA_WITH_AES_256_CCM, `0xc09e` DHE_RSA_WITH_AES_128_CCM, `0xc09f` DHE_RSA_WITH_AES_256_CCM, `0xc0a0` RSA_WITH_AES_128_CCM_8, `0xc0a1` RSA_WITH_AES_256_CCM_8, `0xc0a2` DHE_RSA_WITH_AES_128_CCM_8, `0xc0a3` DHE_RSA_WITH_AES_256_CCM_8.
  - RFC 7251 (TLS 1.2 only, SHA-256 PRF): `0xc0ac` ECDHE_ECDSA_WITH_AES_128_CCM, `0xc0ad` ECDHE_ECDSA_WITH_AES_256_CCM, `0xc0ae` ECDHE_ECDSA_WITH_AES_128_CCM_8, `0xc0af` ECDHE_ECDSA_WITH_AES_256_CCM_8.
  - RC4 (TLS 1.0 to 1.2, HMAC MAC as named): `0x0004` RSA_WITH_RC4_128_MD5, `0x0005` RSA_WITH_RC4_128_SHA, `0xc011` ECDHE_RSA_WITH_RC4_128_SHA, `0xc007` ECDHE_ECDSA_WITH_RC4_128_SHA, `0x0018` DH_anon_WITH_RC4_128_MD5, `0xc016` ECDH_anon_WITH_RC4_128_SHA.
- The PSK CCM suites are out, as ADR-0140 leaves out every PSK family.

## Acceptance criteria

- [ ] `Tls12CipherSuite` has the 12 CCM/CCM8 and 6 RC4 suites above, each with its key exchange, authentication, bulk cipher, MAC, PRF hash and minimum version; a `Tls12CipherSuiteTests` test pins each code point's properties.
- [ ] `Tls12ClientHandshakeTests` complete an in-memory handshake against `Tls12TestServer` on one CCM suite (e.g. `0xc0ac`), one CCM8 suite (e.g. `0xc0a0`) and one RC4 suite (e.g. `0xc011`), and in each case an application-data record protected with the derived keys is decrypted by the other side.
- [ ] A CCM or CCM8 suite is not offered when the maximum version is below TLS 1.2, and a server that selects one at TLS 1.1 or 1.0 is refused with the alert ADR-0154 names for an illegal suite selection (a named test in `Tls12ClientHandshakeFailureTests`).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Whether the RC4 and CCM suites appear in the default ClientHello is a profile question for whichever task builds the per-platform hello; this task makes them available in `Tls12CipherSuite`, not a change to the default offer unless ADR-0140's profiles already list them.

## Log

- 2026-09-28: Created.
