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
completed: 2026-09-28
---
# BL-739 — Hand-build constant-time finite-field Diffie-Hellman for the RFC 3526 and RFC 7919 groups

## Goal

`Curl.Cryptography.UnitLibrary` generates key pairs and computes shared secrets for finite-field Diffie-Hellman in constant time over fixed-width limbs (Montgomery multiplication, a fixed-window or ladder exponentiation with no secret-dependent branch or table index), for the SSH groups (Oakley group 1 and 2 from RFC 2409, groups 14, 16 and 18 from RFC 3526, and a server-chosen group from `diffie-hellman-group-exchange-*`, RFC 4419) and the TLS groups `ffdhe2048` to `ffdhe8192` (RFC 7919), and validates the peer's value as RFC 7919 section 5.1 and RFC 4253 section 8 require.

## Context

- ADR-0118 (BL-669): the BCL has no Diffie-Hellman over integers, and `System.Numerics.BigInteger.ModPow` branches on the exponent's bits, so using it on a secret exponent breaks ADR-0118's constant-time rule. The type is `FiniteFieldDiffieHellman`.
- Consumers: SSH `diffie-hellman-group1-sha1`, `diffie-hellman-group14-sha1`, `diffie-hellman-group14-sha256`, `diffie-hellman-group16-sha512`, `diffie-hellman-group18-sha512` and `diffie-hellman-group-exchange-sha1/sha256` (BL-560's ADR, BL-678); TLS 1.2 `DHE-RSA-*` suites, which OpenSSL's `DEFAULT` list enables (`openssl ciphers -s -v DEFAULT`, OpenSSL 3.5.7, 2026-09-28), and the TLS 1.3 `ffdhe*` groups (BL-703, BL-709).
- Vectors: there are no published known-answer vectors for these groups, so the tests pin (a) the group primes byte for byte against the RFC texts, (b) the NIST CAVP "KAS FFC" known-answer tests for the `dhEphem` scheme where a vector uses one of these primes, and (c) agreement between two hand-built key pairs and against `BigInteger.ModPow` for fixed non-secret test exponents.

## Acceptance criteria

- [x] `Curl.Cryptography.UnitTests` pin every named group's prime and generator against the RFC's hexadecimal text, and two parties' shared secrets agree for every group.
- [x] For fixed test exponents the result equals `BigInteger.ModPow` for every named group, and a peer value of 0, 1, p-1 or p or larger is rejected with the typed failure ADR-0118 names.
- [x] No secret-dependent branch or table index in the exponentiation, stated in the XML docs; the private exponent is zeroed on `Dispose`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Built: internal `MontgomeryModulus` (32-bit limbs, CIOS Montgomery multiplication, masked
  final subtraction, fixed 4-bit window exponentiation that squares four times and always
  multiplies, table look-up reading all 16 entries by mask); public
  `FiniteFieldDiffieHellmanGroup` (named groups `Group1`, `Group2`, `Group14`, `Group16`,
  `Group18`, `Ffdhe2048` to `Ffdhe8192`, and `TryCreate` for group exchange); public
  `FiniteFieldDiffieHellman` (`IDisposable` key pair).
- Primes were extracted from the RFC texts fetched from rfc-editor.org on 2026-09-28. The
  tests pin them to the RFC's hex and, independently, to each RFC's closed formula with pi
  (Machin) or e (series) computed in the test, which catches a transcription error in both.
- Choices made (sensible defaults; an ADR was not written because
  `Documentation/Planning/Decisions` is in BL-695's `touches` while it is in Doing, and
  each choice sits inside ADR-0118's API shape; they are recorded in the library's
  CLAUDE.md):
  - The group is a second public type, `FiniteFieldDiffieHellmanGroup`: a parameter set
    both SSH (hashes p and g in group exchange) and TLS (ServerDHParams) need to read.
  - `Generate` draws a 512-bit exponent with its top bit set: at least twice every named
    group's strength (RFC 7919 asks 400 bits for ffdhe8192) and OpenSSH's twice-the-hash
    size for SHA-512. For a server-chosen p shorter than 65 bytes it draws p's length - 1
    bytes, which keeps 1 < x < p - 1 without a secret comparison.
  - The constructor's x is checked for length only (1 to p's length); its value is the
    caller's to keep in range, since checking it would branch on a secret.
  - Public values and shared secrets are exactly p's length, big-endian, leading zeros
    kept (RFC 7919 section 5.1); SSH `mpint` encoding and TLS 1.2's leading-zero strip
    (RFC 5246 section 8.1.2) are the consumer's.
  - Peer y is validated as 1 < y < p - 1 on public data with `BigInteger`; any length is
    accepted and leading zeros are ignored. `TryCreate` returns false for even p, p < 2^8,
    or g outside 1 < g < p - 1; minimum size and primality are the SSH protocol's check
    (RFC 4419's min/max), not the primitive's.
- NIST CAVP KAS FFC `dhEphem` vectors use generated FIPS 186 domain parameters (FB/FC
  sets), none of them these safe primes, so (b) in Context has no applicable vector; the
  `BigInteger.ModPow` cross-check on public exponents and Mersenne-prime group-exchange
  moduli (61, 89, 107, 127 bits: 2 to 4 limbs, and a 14-byte modulus) stand in.
- Results: 61 new tests (53 for the two public types, 8 in `MontgomeryModulusTests`);
  Curl.Cryptography.UnitTests 365 passed; every fast test in the solution green; Measure-CodeQuality
  reports 100% line, 100% branch, 158 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Constant-time finite-field Diffie-Hellman works in Oakley 1/2, MODP 14/16/18, ffdhe2048-8192 and SSH group-exchange groups, with peer-value validation
