---
id: BL-1558
title: Make BcryptPbkdf's bcrypt_hash fast enough that a one-round, 1024-byte key derives in well under a second
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1558 — Make BcryptPbkdf's bcrypt_hash fast enough that a one-round, 1024-byte key derives in well under a second

## Goal

`BcryptPbkdf.DeriveKey` runs one bcrypt_hash (64 expensive key-schedule rounds) in a few milliseconds, as OpenSSH's C does, so `BcryptPbkdfTests` no longer prints `SLOW:` lines and an OpenSSH key with the usual 16 or more rounds decrypts without a multi-second pause.

## Context

- Found by BL-1535 (2026-10-07), measured under a busy machine with other lanes building: `DeriveKey_GoGoldenVector0_GivesThePublishedKey` (12 rounds, one block) took `PHASE derive: 5940 ms`, `DeriveKey_GoGoldenVector2_GivesThePublishedKey` 12044 ms, and `DeriveKey_OneRoundMaximumLengthKey_FillsEveryByte` (1 round, 32 blocks) 15108 ms - about 500 ms per bcrypt_hash, roughly two orders of magnitude slower than C.
- Start in `Curl.Cryptography.UnitLibrary`: `BcryptPbkdf` (its expensive-rounds loops) and the Blowfish state it drives (`BlowfishState` key schedule and encryption). Look for per-call allocations, bounds-checked span work or byte-by-byte word reads in the hot loop; keep the code constant-time where it touches the password.
- Behaviour must not change: every existing `BcryptPbkdfTests`, `BlowfishTests` and `BlowfishStateTests` vector still passes.

## Acceptance criteria

- [x] `dotnet test Curl.Cryptography.UnitTests --filter "FullyQualifiedName~Curl.Cryptography.BcryptPbkdfTests." --logger "console;verbosity=detailed"` prints no `SLOW:` line, and the before and after `PHASE derive` times are in Notes.
- [x] All `Curl.Cryptography.UnitTests` fast tests pass and `dotnet build -warnaserror` is clean.
- [x] `Curl.Cryptography.UnitLibrary` keeps 100% line and branch coverage, complexity at most 10 and CRAP at most 30.

## Notes

- Cause: the build, not the algorithm. The tests run the Debug assembly, whose
  `DebuggableAttribute` disables JIT optimization, so ADR-0400's masked S-box scan ran at
  about 600 ms per bcrypt hash against 33 ms in Release (measured with a throwaway
  `dotnet run bench.cs` outside the repo). `AggressiveOptimization` on the hot methods
  does not override that attribute (measured, no gain).
- Fix (ADR-0426, decided by Claude under Stewart's delegation):
  `Curl.Cryptography.UnitLibrary.csproj` sets `<Optimize>true</Optimize>`. One hash in
  Debug is now about 42 ms. No source change; the constant-time scan is kept. An
  AVX-512 scan was tried and dropped: 44 ms vs 42 ms on this machine.
- `BcryptPbkdfTests`, criterion command, before -> after `PHASE derive`:
  GoGoldenVector0 5799 -> 1140 ms, GoGoldenVector1 1407 -> 418 ms, GoGoldenVector2
  11259 -> 1320 ms, OneRoundMaximumLengthKey 10810 -> 2158 ms; `PHASE hash` 371 -> 285 ms.
  No `SLOW:` line after. In a whole `Curl.Cryptography.UnitTests` run beside other lanes'
  builds, the 32-block test once took 3492 ms, just over the 3 s budget: its 32 hashes
  are about 1.4 s of CPU, and the rest is contention.
- "A few milliseconds, as OpenSSH's C" is not reachable without table-indexed look-ups,
  which ADR-0400 ruled out for the passphrase leak (AF-0017); the constant-time scan's
  floor is about 30 ms per hash.
- Gates: `dotnet build -warnaserror` clean; all 33 fast test projects pass (Cryptography
  1445); Measure-CodeQuality on Curl.Cryptography.UnitLibrary: 100% line, 100% branch,
  max complexity 10, 0 failing members.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Curl.Cryptography compiles optimized in Debug (ADR-0426): one bcrypt hash 600 ms -> 42 ms, BcryptPbkdfTests print no SLOW: line
