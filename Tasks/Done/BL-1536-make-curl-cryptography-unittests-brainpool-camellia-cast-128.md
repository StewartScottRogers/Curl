---
id: BL-1536
title: Make Curl.Cryptography.UnitTests' Brainpool, Camellia, CAST-128, ChaCha20 and ConstantTime tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Cryptography.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1536 — Make Curl.Cryptography.UnitTests' Brainpool, Camellia, CAST-128, ChaCha20 and ConstantTime tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Cryptography.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `BrainpoolEcdhTests.cs`, `BrainpoolEcdsaTests.cs`, `CamelliaTests.cs`, `Cast128Tests.cs`, `ChaCha20Tests.cs`, `ConstantTimeTests.cs` (73 test methods, counted 2026-10-07).

## Context

- Split from BL-1464 (one per range of files, as BL-1463 was split); BL-1464 keeps the whole-project checks and depends on this task. Read BL-1464's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR (ADR-0417); add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- What matters here: the test vector's source (RFC and section) as an `ARRANGE` line, keys, nonces and inputs as `BYTES`, and a `DIFF` line for every digest, MAC, ciphertext, shared secret or signature compared.
- The test classes are in namespace `Curl.Cryptography` (not `Curl.Cryptography.UnitTests`), which the filter below uses.

## Acceptance criteria

- [x] `dotnet test Curl.Cryptography.UnitTests --filter "FullyQualifiedName~Curl.Cryptography.BrainpoolEcdhTests.|FullyQualifiedName~Curl.Cryptography.BrainpoolEcdsaTests.|FullyQualifiedName~Curl.Cryptography.CamelliaTests.|FullyQualifiedName~Curl.Cryptography.Cast128Tests.|FullyQualifiedName~Curl.Cryptography.ChaCha20Tests.|FullyQualifiedName~Curl.Cryptography.ConstantTimeTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Cryptography.UnitTests -warnaserror` is clean and `dotnet test Curl.Cryptography.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Cryptography.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts in the six files, before -> after: `Assert.` 136 -> 136, `[TestMethod` 73 -> 73, `[DataRow(` 106 -> 106 (per file unchanged: Ecdh 29/15/23, Ecdsa 25/13/18, Camellia 26/10/17, Cast128 27/12/12, ChaCha20 17/13/20, ConstantTime 12/10/16).
- The detailed run executed 147 tests (73 methods with their data rows, Integration included), printed 147 `END` lines, none with a zero arrange, act or assert count; all passed. The fast run of the project: 1335 passed.
- Where an assertion wrapped the call under test (`Assert.IsTrue(Verify(...))`), the result is now taken into a local first and the same assertion made on it, so the `ACT` line can show it; no assertion's condition changed. `BrainpoolEcdsaTests.AssertEveryWycheproofVector` became an instance method (it needs `TestContext`) and its loop moved to `VerifyEveryLine` so it can sit inside a `PHASE`.
- Throws-only tests record the exception type as `ACT` and `ASSERT`; vector tests name the RFC section (or Wycheproof file / OpenSSL recording) as `ARRANGE vector source`, keys, nonces and inputs as `BYTES`, and a `DIFF` for every ciphertext, key stream block, public key, shared secret and signature compared.
- `SLOW:` lines (with `PHASE` breakdown), while other lanes were building:
  - `BrainpoolEcdsaTests.VerifyHash_EveryWycheproofP256r1Vector_GivesItsExpectedResult` (fast run): 3647 ms, `PHASE verify every vector: 3646 ms`. Follow-up: BL-1559.
  - `BrainpoolEcdsaTests.VerifyHash_EveryWycheproofP384r1AndP512r1Vector_GivesItsExpectedResult` (Integration): P384r1 9701 ms (`PHASE verify every vector: 9676 ms`), P512r1 24613 ms (`PHASE verify every vector: 24611 ms`). Covered by BL-1559's work on the same code.
  - `Cast128Tests.EncryptBlock_Rfc2144AppendixB2FullMaintenanceTest_GivesThePublishedAAndB` (Integration): 1436217 ms, `PHASE million iterations: 1436216 ms` - about 0.7 ms per key schedule. Already filed as BL-1525.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. All 73 test methods in the Brainpool, Camellia, CAST-128, ChaCha20 and ConstantTime test files write vector sources, BYTES inputs, ACT results and DIFF/ASSERT lines through TestDiagnostics
