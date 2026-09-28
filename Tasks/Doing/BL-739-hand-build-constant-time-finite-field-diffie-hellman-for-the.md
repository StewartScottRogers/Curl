---
id: BL-739
title: Hand-build constant-time finite-field Diffie-Hellman for the RFC 3526 and RFC 7919 groups
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-670]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-739 — Hand-build constant-time finite-field Diffie-Hellman for the RFC 3526 and RFC 7919 groups

## Goal

`Curl.Cryptography.UnitLibrary` generates key pairs and computes shared secrets for finite-field Diffie-Hellman in constant time over fixed-width limbs (Montgomery multiplication, a fixed-window or ladder exponentiation with no secret-dependent branch or table index), for the SSH groups (Oakley group 1 and 2 from RFC 2409, groups 14, 16 and 18 from RFC 3526, and a server-chosen group from `diffie-hellman-group-exchange-*`, RFC 4419) and the TLS groups `ffdhe2048` to `ffdhe8192` (RFC 7919), and validates the peer's value as RFC 7919 section 5.1 and RFC 4253 section 8 require.

## Context

- ADR-0118 (BL-669): the BCL has no Diffie-Hellman over integers, and `System.Numerics.BigInteger.ModPow` branches on the exponent's bits, so using it on a secret exponent breaks ADR-0118's constant-time rule. The type is `FiniteFieldDiffieHellman`.
- Consumers: SSH `diffie-hellman-group1-sha1`, `diffie-hellman-group14-sha1`, `diffie-hellman-group14-sha256`, `diffie-hellman-group16-sha512`, `diffie-hellman-group18-sha512` and `diffie-hellman-group-exchange-sha1/sha256` (BL-560's ADR, BL-678); TLS 1.2 `DHE-RSA-*` suites, which OpenSSL's `DEFAULT` list enables (`openssl ciphers -s -v DEFAULT`, OpenSSL 3.5.7, 2026-09-28), and the TLS 1.3 `ffdhe*` groups (BL-703, BL-709).
- Vectors: there are no published known-answer vectors for these groups, so the tests pin (a) the group primes byte for byte against the RFC texts, (b) the NIST CAVP "KAS FFC" known-answer tests for the `dhEphem` scheme where a vector uses one of these primes, and (c) agreement between two hand-built key pairs and against `BigInteger.ModPow` for fixed non-secret test exponents.

## Acceptance criteria

- [ ] `Curl.Cryptography.UnitTests` pin every named group's prime and generator against the RFC's hexadecimal text, and two parties' shared secrets agree for every group.
- [ ] For fixed test exponents the result equals `BigInteger.ModPow` for every named group, and a peer value of 0, 1, p-1 or p or larger is rejected with the typed failure ADR-0118 names.
- [ ] No secret-dependent branch or table index in the exponentiation, stated in the XML docs; the private exponent is zeroed on `Dispose`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
