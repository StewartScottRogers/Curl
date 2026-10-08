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
completed: 2026-10-07
---
# BL-1656 — Speed up the constant-time FFDHE exponentiation for ffdhe6144 and ffdhe8192

## Goal

`FiniteFieldDiffieHellman` generates a key and computes a shared secret on ffdhe8192 fast enough that a TLS 1.3 handshake on that group (`--curves ffdhe8192`) is not seconds slower than real curl, without giving up its constant-time exponentiation (BL-739).

## Context

- Found by BL-1632's diagnostics: `Curl.Tls.KeyShareTests.TwoSharesOnAGroupAgreeOnTheSharedSecret` printed `SLOW:` for ffdhe6144 (0x0103) and ffdhe8192 (0x0104) on a loaded lane machine: ffdhe6144 `PHASE key generation: 1777 ms`, `PHASE agreement: 1862 ms` (4678 ms in all); ffdhe8192 `PHASE key generation: 4142 ms`, `PHASE agreement: 2556 ms` (7639 ms in all). Each phase is two modular exponentiations, so one 8192-bit exponentiation costs about 1.3 to 2 s; OpenSSL's takes tens of milliseconds.
- The code is `Curl.Cryptography.UnitLibrary/FiniteFieldDiffieHellman.cs`, behind `Curl.Tls.UnitLibrary/FfdheKeyShare.cs`. Look first at the exponentiation loop (Montgomery multiplication with a fixed window, a private exponent of the RFC 7919 recommended size rather than the full group size, avoiding per-step `BigInteger` allocation), and keep it constant-time.
- BCL only; the Cryptography library's quality gates apply.

## Acceptance criteria

- [x] Measured on the same machine before and after (numbers in Notes), one ffdhe8192 key generation plus agreement in `FiniteFieldDiffieHellman` is at least 4 times faster than before.
- [x] The exponentiation still takes the same sequence of operations whatever the exponent's bits, stated in a doc comment and checked by a test.
- [x] `Curl.Tls.KeyShareTests.TwoSharesOnAGroupAgreeOnTheSharedSecret` prints no `SLOW:` line for any group when `Curl.Tls.UnitTests` runs alone.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes; `Curl.Cryptography.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- Plan: the generated exponent was already 512 bits and the loop already a fixed 4-bit
  window over Montgomery multiplication, so the work went into the multiplication itself.
  `MontgomeryModulus.Exponentiate` now converts to 64-bit limbs at its edges and runs a
  finely integrated 64-bit Montgomery multiplication (`Math.BigMul`) and a separate
  squaring (cross products once, then reduction) in its own radix R' (ADR-0429). The
  32-bit public API, its callers (RSA, DSA, brainpool) and their scratch sizes are
  unchanged.
- The seconds the task saw were mostly cold JIT code: the old helpers (`AddProduct`,
  `ReduceOneLimb`) stayed at tier 0 in a busy test host. The hot 64-bit routines carry
  `AggressiveOptimization`, so the first call runs optimized.
- Carries: `Unsafe.BitCast<bool, byte>(sum < left)` (IL `clt.un`, JIT `setb`, checked with
  `DOTNET_JitDisasm=MultiplyWide`), not a branch.
- Measurements, this lane machine under other lanes' load, four ffdhe8192 exponentiations
  (two key pairs' public values and both shared secrets, i.e. one key generation plus
  agreement on each side), Release, HEAD's library copied out as "before":
  - first call in a fresh process, four runs each: before 444, 505, 828, 492 ms; after
    107, 380 (load spike), 117, 117 ms - about 4.3 times faster at the median.
  - warm (best of 12 rounds), three alternating runs: before 186, 184, 193 ms; after 84,
    96, 82 ms - about 2.2 times faster.
  The first-call figure is what one curl run pays and what the test saw; native AOT curl
  sees the warm figure. A further warm gain (64-bit limbs for `Multiply` and the brainpool
  curves, or a fixed-base comb for g^x) is left for a follow-up.
- `KeyShareTests.TwoSharesOnAGroupAgreeOnTheSharedSecret` run alone in `Curl.Tls.UnitTests`:
  no `SLOW:`; ffdhe8192 key generation 119 ms, agreement 41 ms.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. ffdhe8192 exponentiation runs on 64-bit limbs with its own squaring: first-call key generation plus agreement about 4.3x faster, still constant-time, no SLOW: in KeyShareTests
