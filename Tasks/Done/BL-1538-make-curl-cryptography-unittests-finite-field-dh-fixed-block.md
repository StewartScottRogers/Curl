---
id: BL-1538
title: Make Curl.Cryptography.UnitTests' finite-field DH, fixed-block hash, GCM, HMAC-RIPEMD-160, HPKE and MD4 tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Cryptography.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1538 — Make Curl.Cryptography.UnitTests' finite-field DH, fixed-block hash, GCM, HMAC-RIPEMD-160, HPKE and MD4 tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Cryptography.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `FiniteFieldDiffieHellmanGroupTests.cs`, `FiniteFieldDiffieHellmanTests.cs`, `FixedBlockHmacTests.cs`, `FixedBlockMerkleDamgardTests.cs`, `GaloisCounterModeTests.cs`, `HmacRipemd160Tests.cs`, `HpkeContextTests.cs`, `HpkeDhkemTests.cs`, `HpkeTests.cs`, `Md4Tests.cs` (69 test methods, counted 2026-10-07).

## Context

- Split from BL-1464 (one per range of files, as BL-1463 was split); BL-1464 keeps the whole-project checks and depends on this task. Read BL-1464's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR (ADR-0417); add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- What matters here: the test vector's source (RFC and section) as an `ARRANGE` line, keys, nonces and inputs as `BYTES`, and a `DIFF` line for every digest, MAC, ciphertext, shared secret or signature compared.
- The test classes are in namespace `Curl.Cryptography` (not `Curl.Cryptography.UnitTests`), which the filter below uses.

## Acceptance criteria

- [x] `dotnet test Curl.Cryptography.UnitTests --filter "FullyQualifiedName~Curl.Cryptography.FiniteFieldDiffieHellmanGroupTests.|FullyQualifiedName~Curl.Cryptography.FiniteFieldDiffieHellmanTests.|FullyQualifiedName~Curl.Cryptography.FixedBlockHmacTests.|FullyQualifiedName~Curl.Cryptography.FixedBlockMerkleDamgardTests.|FullyQualifiedName~Curl.Cryptography.GaloisCounterModeTests.|FullyQualifiedName~Curl.Cryptography.HmacRipemd160Tests.|FullyQualifiedName~Curl.Cryptography.HpkeContextTests.|FullyQualifiedName~Curl.Cryptography.HpkeDhkemTests.|FullyQualifiedName~Curl.Cryptography.HpkeTests.|FullyQualifiedName~Curl.Cryptography.Md4Tests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Cryptography.UnitTests -warnaserror` is clean and `dotnet test Curl.Cryptography.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Cryptography.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts per file, before -> after (`Assert.` / `[TestMethod` / `[DataRow(`): FiniteFieldDiffieHellmanGroupTests 10->10 / 3->3 / 16->16; FiniteFieldDiffieHellmanTests 31->31 / 11->11 / 9->9; FixedBlockHmacTests 4->4 / 4->4 / 10->10; FixedBlockMerkleDamgardTests 5->5 / 5->5 / 0->0; GaloisCounterModeTests 14->14 / 5->5 / 21->21; HmacRipemd160Tests 9->9 / 9->9 / 11->11; HpkeContextTests 31->31 / 11->11 / 4->4; HpkeDhkemTests 9->9 / 4->4 / 7->7; HpkeTests 31->31 / 11->11 / 10->10; Md4Tests 6->6 / 6->6 / 16->16. Totals 150 / 69 / 104, unchanged.
- The acceptance filter runs 209 tests (data rows and dynamic data expanded); all 209 print an `END` line with non-zero arrange, act and assert counts; 209 passed. The whole project's fast run: 1335 passed.
- Loops over many lengths or random inputs (fixed-block hash and HMAC, GCM against AesGcm) write a `PHASE` and one `ACT` count and `ASSERT` summary, and write `BYTES` and `DIFF` lines only for the failing iteration, so a passing run stays short (a choice: hundreds of equal DIFF lines per test would bury the useful ones).
- No test printed a `SLOW:` line. The longest `PHASE` was "public values and shared secrets" in the 8192-bit groups of FiniteFieldDiffieHellmanTests, 2282 ms on a machine shared with other lanes; under the 3000 ms budget, so no follow-up task.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. The ten files' 69 tests write ARRANGE, ACT, ASSERT/DIFF, BYTES and PHASE diagnostics; 209 runs print non-zero END counts
