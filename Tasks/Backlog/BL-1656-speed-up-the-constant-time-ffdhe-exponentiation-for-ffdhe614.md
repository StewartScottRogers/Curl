---
id: BL-1656
title: Speed up the constant-time FFDHE exponentiation for ffdhe6144 and ffdhe8192
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-10-07
completed:
---
# BL-1656 — Speed up the constant-time FFDHE exponentiation for ffdhe6144 and ffdhe8192

## Goal

`FiniteFieldDiffieHellman` generates a key and computes a shared secret on ffdhe8192 fast enough that a TLS 1.3 handshake on that group (`--curves ffdhe8192`) is not seconds slower than real curl, without giving up its constant-time exponentiation (BL-739).

## Context

- Found by BL-1632's diagnostics: `Curl.Tls.KeyShareTests.TwoSharesOnAGroupAgreeOnTheSharedSecret` printed `SLOW:` for ffdhe6144 (0x0103) and ffdhe8192 (0x0104) on a loaded lane machine: ffdhe6144 `PHASE key generation: 1777 ms`, `PHASE agreement: 1862 ms` (4678 ms in all); ffdhe8192 `PHASE key generation: 4142 ms`, `PHASE agreement: 2556 ms` (7639 ms in all). Each phase is two modular exponentiations, so one 8192-bit exponentiation costs about 1.3 to 2 s; OpenSSL's takes tens of milliseconds.
- The code is `Curl.Cryptography.UnitLibrary/FiniteFieldDiffieHellman.cs`, behind `Curl.Tls.UnitLibrary/FfdheKeyShare.cs`. Look first at the exponentiation loop (Montgomery multiplication with a fixed window, a private exponent of the RFC 7919 recommended size rather than the full group size, avoiding per-step `BigInteger` allocation), and keep it constant-time.
- BCL only; the Cryptography library's quality gates apply.

## Acceptance criteria

- [ ] Measured on the same machine before and after (numbers in Notes), one ffdhe8192 key generation plus agreement in `FiniteFieldDiffieHellman` is at least 4 times faster than before.
- [ ] The exponentiation still takes the same sequence of operations whatever the exponent's bits, stated in a doc comment and checked by a test.
- [ ] `Curl.Tls.KeyShareTests.TwoSharesOnAGroupAgreeOnTheSharedSecret` prints no `SLOW:` line for any group when `Curl.Tls.UnitTests` runs alone.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes; `Curl.Cryptography.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-10-07: Created.
