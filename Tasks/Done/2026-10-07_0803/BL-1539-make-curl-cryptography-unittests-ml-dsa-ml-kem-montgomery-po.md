---
id: BL-1539
title: Make Curl.Cryptography.UnitTests' ML-DSA, ML-KEM, Montgomery, Poly1305, RC4, RIPEMD-160 and RSA-CRT tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Cryptography.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1539 — Make Curl.Cryptography.UnitTests' ML-DSA, ML-KEM, Montgomery, Poly1305, RC4, RIPEMD-160 and RSA-CRT tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Cryptography.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `MlDsaEncodingTests.cs`, `MlDsaTests.cs`, `MlKemTests.cs`, `MontgomeryModulusTests.cs`, `Poly1305Tests.cs`, `Rc4Tests.cs`, `Ripemd160Tests.cs`, `RsaCrtPrivateKeyTests.cs` (66 test methods, counted 2026-10-07).

## Context

- Split from BL-1464 (one per range of files, as BL-1463 was split); BL-1464 keeps the whole-project checks and depends on this task. Read BL-1464's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR (ADR-0417); add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- What matters here: the test vector's source (RFC and section) as an `ARRANGE` line, keys, nonces and inputs as `BYTES`, and a `DIFF` line for every digest, MAC, ciphertext, shared secret or signature compared.
- The test classes are in namespace `Curl.Cryptography` (not `Curl.Cryptography.UnitTests`), which the filter below uses.

## Acceptance criteria

- [x] `dotnet test Curl.Cryptography.UnitTests --filter "FullyQualifiedName~Curl.Cryptography.MlDsaEncodingTests.|FullyQualifiedName~Curl.Cryptography.MlDsaTests.|FullyQualifiedName~Curl.Cryptography.MlKemTests.|FullyQualifiedName~Curl.Cryptography.MontgomeryModulusTests.|FullyQualifiedName~Curl.Cryptography.Poly1305Tests.|FullyQualifiedName~Curl.Cryptography.Rc4Tests.|FullyQualifiedName~Curl.Cryptography.Ripemd160Tests.|FullyQualifiedName~Curl.Cryptography.RsaCrtPrivateKeyTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Cryptography.UnitTests -warnaserror` is clean and `dotnet test Curl.Cryptography.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Cryptography.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts in the eight files, before -> after (Assert. / [TestMethod / [DataRow( ): MlDsaEncodingTests 7/2/4 -> 7/2/4, MlDsaTests 38/15/39 -> 38/15/39, MlKemTests 40/11/33 -> 40/11/33, MontgomeryModulusTests 8/7/19 -> 8/7/19, Poly1305Tests 7/7/16 -> 7/7/16, Rc4Tests 15/9/256 -> 15/9/256, Ripemd160Tests 7/7/17 -> 7/7/17, RsaCrtPrivateKeyTests 12/8/7 -> 12/8/7. Totals 134/66/391 before and after.
- The acceptance filter ran 424 tests (66 methods with their data rows): 424 END lines, all Passed, none with a zero arrange, act or assert count.
- No test printed a SLOW: line. The longest PHASE was RIPEMD-160 one million "a" at 306 ms; RSA-CRT private exponent 55 to 73 ms per blinding, 2048-bit key generation 41 ms. No follow-up task needed.
- Choice: a test whose Act is a thrown exception writes `ACT exception: <type name>` from the exception Assert.ThrowsExactly returns, and multi-call throw tests write one ACT per call, so no Assert. call was removed or folded into a helper; type names, not messages, are printed so nothing depends on the OS or culture.
- Choice: the random-key tests (RSA-CRT 2048-bit, ML-KEM and ML-DSA round trips) print descriptions and results, not the random keys, which differ every run.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. All 66 tests in the eight files write descriptive diagnostics; build clean, fast tests green
