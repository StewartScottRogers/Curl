---
id: BL-1535
title: Make Curl.Cryptography.UnitTests' AEAD, AES, ARIA, bcrypt-pbkdf and Blowfish tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Cryptography.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1535 — Make Curl.Cryptography.UnitTests' AEAD, AES, ARIA, bcrypt-pbkdf and Blowfish tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Cryptography.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `AeadAesCcmTests.cs`, `AeadAriaGcmTests.cs`, `AeadChaCha20Poly1305Tests.cs`, `AesCbcCtsTests.cs`, `AesCtrTests.cs`, `AriaTests.cs`, `BcryptPbkdfTests.cs`, `BlowfishStateTests.cs`, `BlowfishTests.cs` (75 test methods, counted 2026-10-07).

## Context

- Split from BL-1464 (one per range of files, as BL-1463 was split); BL-1464 keeps the whole-project checks and depends on this task. Read BL-1464's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR (ADR-0417); add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- What matters here: the test vector's source (RFC and section) as an `ARRANGE` line, keys, nonces and inputs as `BYTES`, and a `DIFF` line for every digest, MAC, ciphertext, shared secret or signature compared.
- The test classes are in namespace `Curl.Cryptography` (not `Curl.Cryptography.UnitTests`), which the filter below uses.

## Acceptance criteria

- [x] `dotnet test Curl.Cryptography.UnitTests --filter "FullyQualifiedName~Curl.Cryptography.AeadAesCcmTests.|FullyQualifiedName~Curl.Cryptography.AeadAriaGcmTests.|FullyQualifiedName~Curl.Cryptography.AeadChaCha20Poly1305Tests.|FullyQualifiedName~Curl.Cryptography.AesCbcCtsTests.|FullyQualifiedName~Curl.Cryptography.AesCtrTests.|FullyQualifiedName~Curl.Cryptography.AriaTests.|FullyQualifiedName~Curl.Cryptography.BcryptPbkdfTests.|FullyQualifiedName~Curl.Cryptography.BlowfishStateTests.|FullyQualifiedName~Curl.Cryptography.BlowfishTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Cryptography.UnitTests -warnaserror` is clean and `dotnet test Curl.Cryptography.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Cryptography.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- All 75 test methods (224 test cases with data rows) in the nine files take `TestContext`, call `TestDiagnostics.For`, and write the vector's source as `ARRANGE`, keys, nonces and inputs as `BYTES`, results as `ACT`, and a `DIFF` for every ciphertext, tag, keystream, derived key and plaintext compared (`ASSERT` for booleans, counts and exception `ParamName`s). Helper signature changes: `AeadAesCcmTests.AssertSealsAndOpens` and `BcryptPbkdfTests.AssertDerives` take the test's `TestDiagnostics` first.
- Counts in the nine files, before -> after: `Assert.` 146 -> 146, `[TestMethod` 75 -> 75, `[DataRow(` 182 -> 182.
- The acceptance filter with the detailed logger prints 224 `END` lines, none with a zero count; all 1335 fast tests in the project pass.
- `SLOW:` lines (measured with other lanes building), all in `BcryptPbkdfTests`, each one phase:
  - `DeriveKey_GoGoldenVector0_GivesThePublishedKey`: `PHASE derive: 5996 ms` (12 rounds, one block).
  - `DeriveKey_GoGoldenVector2_GivesThePublishedKey`: `PHASE derive: 11928 ms`.
  - `DeriveKey_OneRoundMaximumLengthKey_FillsEveryByte`: `PHASE derive: 11394 ms` (1 round, 32 blocks).
  - That is about 400 to 500 ms per bcrypt_hash, roughly a hundred times C's speed: a real performance problem, filed as BL-1541.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every AEAD, AES, ARIA, bcrypt-pbkdf and Blowfish test writes ARRANGE, ACT and ASSERT or DIFF diagnostics
