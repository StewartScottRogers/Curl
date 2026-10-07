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
completed:
---
# BL-1558 — Make BcryptPbkdf's bcrypt_hash fast enough that a one-round, 1024-byte key derives in well under a second

## Goal

`BcryptPbkdf.DeriveKey` runs one bcrypt_hash (64 expensive key-schedule rounds) in a few milliseconds, as OpenSSH's C does, so `BcryptPbkdfTests` no longer prints `SLOW:` lines and an OpenSSH key with the usual 16 or more rounds decrypts without a multi-second pause.

## Context

- Found by BL-1535 (2026-10-07), measured under a busy machine with other lanes building: `DeriveKey_GoGoldenVector0_GivesThePublishedKey` (12 rounds, one block) took `PHASE derive: 5940 ms`, `DeriveKey_GoGoldenVector2_GivesThePublishedKey` 12044 ms, and `DeriveKey_OneRoundMaximumLengthKey_FillsEveryByte` (1 round, 32 blocks) 15108 ms - about 500 ms per bcrypt_hash, roughly two orders of magnitude slower than C.
- Start in `Curl.Cryptography.UnitLibrary`: `BcryptPbkdf` (its expensive-rounds loops) and the Blowfish state it drives (`BlowfishState` key schedule and encryption). Look for per-call allocations, bounds-checked span work or byte-by-byte word reads in the hot loop; keep the code constant-time where it touches the password.
- Behaviour must not change: every existing `BcryptPbkdfTests`, `BlowfishTests` and `BlowfishStateTests` vector still passes.

## Acceptance criteria

- [ ] `dotnet test Curl.Cryptography.UnitTests --filter "FullyQualifiedName~Curl.Cryptography.BcryptPbkdfTests." --logger "console;verbosity=detailed"` prints no `SLOW:` line, and the before and after `PHASE derive` times are in Notes.
- [ ] All `Curl.Cryptography.UnitTests` fast tests pass and `dotnet build -warnaserror` is clean.
- [ ] `Curl.Cryptography.UnitLibrary` keeps 100% line and branch coverage, complexity at most 10 and CRAP at most 30.

## Notes

## Log

- 2026-10-07: Created.
