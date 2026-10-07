---
id: BL-1525
title: Make X25519, X448 and CAST-128 fast enough that their full RFC iteration tests take minutes, not half an hour
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-10-06
completed:
---
# BL-1525 — Make X25519, X448 and CAST-128 fast enough that their full RFC iteration tests take minutes, not half an hour

## Goal

`Curl.Cryptography.UnitLibrary`'s X25519, X448 and CAST-128 run at least five times faster, still constant-time and still bit-exact. Their Integration tests that iterate the RFC vectors in full then finish in minutes, not the 37 minutes they take together today, and every X25519 and X448 key exchange in a TLS, SSH or QUIC handshake gets faster with them.

## Context

- Stewart's request, 2026-10-06: find why the Cryptography Integration tests take 27 minutes and fix it. The new `Integration tests` workflow (`.github/workflows/integration.yml`) runs them on all three platforms on every push, and these tests are almost all of its time.
- Measured 2026-10-06 on Stewart's Windows machine, Release build, `dotnet test -c Release --filter "TestCategory=Integration" --logger trx` (durations from the TRX):
  - `X25519Tests.TryComputeSharedSecret_Rfc7748Section52MillionIterations_GivesTheExpectedK`: 27 min 38 s. That is about 1.66 ms per X25519 operation; well-tuned 64-bit implementations take tens of microseconds.
  - `X448Tests.TryComputeSharedSecret_Rfc7748Section52MillionIterations_GivesTheExpectedK`: 8 min 15 s, about 0.49 ms per X448 operation.
  - `Cast128Tests.EncryptBlock_Rfc2144AppendixB2FullMaintenanceTest_GivesThePublishedAAndB`: 1 min 42 s for RFC 2144 B.2's million iterations, which rekey on every iteration.
  - The other 23 Integration tests together: under 10 s.
- Likely causes, to confirm with a profile before changing anything:
  - `Field25519` and `Field448` hold an element as 16-bit limbs in `long`s (16 and 28 limbs, TweetNaCl style), so a field multiplication is 256 or 784 limb products plus carries. 51-bit limbs for 2^255-19 (5 limbs, `UInt128` or `Math.BigMul` products) and 56-bit limbs for 2^448-2^224-1 (8 limbs) are the usual fast representations.
  - CAST-128: look at the key schedule (`ApplyKeyScheduleRow`, `KeyScheduleRows`), since B.2 rekeys every iteration, and at any per-call allocation (`subkeys` is a `uint[]` per instance).
- Constraints that do not move:
  - Every member stays constant-time: no branch or index that depends on a secret, conditional moves as masks. The class doc comments say so and `audit-security` checks it.
  - No allocation in the field arithmetic.
  - Base class library only; no intrinsics that do not exist on every platform Curl ships for (x64 and Arm64 on Windows, Linux and macOS) unless a portable path stays beside them.
  - Quality gates at 100% line and branch coverage, complexity at most 10, CRAP at most 30.
- Callers to keep working unchanged: Ed25519 also uses `Field25519`, and X25519 and X448 are reached from TLS (`Curl.Networking`), SSH and QUIC. Their public surface does not change.
- BL-1498 (adversarial tests for this library) depends on this task, so it attacks the faster code.

## Acceptance criteria

- [ ] Notes record a profile, or per-operation timings, of X25519, X448 and CAST-128 before the change, naming where the time went.
- [ ] Every existing test in `Curl.Cryptography.UnitTests` passes unchanged, Integration tests included (`dotnet test Curl.Cryptography.UnitTests -c Release --filter "TestCategory=Integration"`), along with the fast tests of every project that uses the library (`dotnet test --filter "TestCategory!=Integration"`).
- [ ] On the same machine and command as Context, the two `Rfc7748Section52MillionIterations` tests and `EncryptBlock_Rfc2144AppendixB2FullMaintenanceTest_GivesThePublishedAAndB` are each at least five times faster than the times in Context. Before and after durations are recorded in Notes.
- [ ] The changed field and cipher code has no secret-dependent branch or table index: each changed class's doc comment states it, and the review stage checks it.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports `Curl.Cryptography.UnitLibrary` at 100% line and branch coverage with no member over complexity 10 or CRAP 30, and `dotnet build -warnaserror` is clean.
- [ ] If a test's own work, not the library, turns out to be a large share of its time, Notes say so and how it was fixed, without dropping or weakening any iteration the RFC specifies.

## Notes

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
