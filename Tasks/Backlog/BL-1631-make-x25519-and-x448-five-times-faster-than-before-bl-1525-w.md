---
id: BL-1631
title: Make X25519 and X448 five times faster than before BL-1525 with 64-bit-only field arithmetic
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-10-07
completed:
---
# BL-1631 — Make X25519 and X448 five times faster than before BL-1525 with 64-bit-only field arithmetic

## Goal

`Curl.Cryptography.UnitLibrary`'s X25519 and X448 key exchanges run at least five times faster than the sources from before BL-1525 (commit `b51e4e59f`'s `Curl.Cryptography.UnitLibrary`), still constant-time and bit-exact, so their RFC 7748 section 5.2 million-iteration Integration tests take minutes.

## Context

- Split from BL-1525 (2026-10-07), which landed CAST-128's speed-up (about 7x) and the first curve speed-up: `Field25519` on 5 x 51-bit limbs, `Field448` on 8 x 56-bit limbs, both accumulating 128-bit column sums in `Accumulator128`; bench ratio against the old sources about 2.9x for X25519 and 2.6x for X448. Read BL-1525's Notes first: they hold every measurement, the JIT disassembly findings and the bench method.
- What still costs time (BL-1525 lane 4): `Math.BigMul`'s `out ulong` low half goes through memory and the ten 128-bit accumulators spill; `Accumulator128.AddHalves` computes each carry with about six bit operations.
- Likely next steps: 64-bit-only arithmetic whose products need no 128-bit sum - radix 2^25.5 in 10 limbs for 2^255-19 (ref10's `fe_mul`/`fe_sq`), and 16 x 28-bit limbs with Hamburg's Karatsuba for 2^448-2^224-1 - or `X86.Bmi2.X64.MultiplyNoFlags` / `ArmBase.Arm64.MultiplyHigh` with the portable path kept beside them.
- The machine running the dark factory is loaded by up to nine lanes, so absolute timings drift by 2x. Judge speed by the ratio of the old and new sources benchmarked back to back in one run (BL-1525's bench: two throwaway console projects in %TEMP% compiling the library's `*.cs`, Release, best of 5 rounds).
- Constraints: no secret-dependent branch or index, no allocation in field arithmetic, BCL only, portable on x64 and Arm64; Ed25519/Edwards25519 also use `Field25519` and must keep working unchanged.

## Acceptance criteria

- [ ] Notes record a back-to-back Release benchmark of the pre-BL-1525 sources and the new ones in one run, showing X25519 and X448 each at least 5x faster per operation.
- [ ] Every test in `Curl.Cryptography.UnitTests` passes unchanged, Integration tests included (`dotnet test Curl.Cryptography.UnitTests -c Release --filter "TestCategory=Integration"`), with their durations recorded in Notes, and the fast tests of the whole solution pass.
- [ ] Each changed field class's doc comment states it has no secret-dependent branch or index, and the review stage checks it.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage with no member over complexity 10 or CRAP 30, and `dotnet build -warnaserror` is clean.

## Notes

## Log

- 2026-10-07: Created.
