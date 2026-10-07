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
completed:
---
# BL-1539 — Make Curl.Cryptography.UnitTests' ML-DSA, ML-KEM, Montgomery, Poly1305, RC4, RIPEMD-160 and RSA-CRT tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Cryptography.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `MlDsaEncodingTests.cs`, `MlDsaTests.cs`, `MlKemTests.cs`, `MontgomeryModulusTests.cs`, `Poly1305Tests.cs`, `Rc4Tests.cs`, `Ripemd160Tests.cs`, `RsaCrtPrivateKeyTests.cs` (66 test methods, counted 2026-10-07).

## Context

- Split from BL-1464 (one per range of files, as BL-1463 was split); BL-1464 keeps the whole-project checks and depends on this task. Read BL-1464's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR (ADR-0417); add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- What matters here: the test vector's source (RFC and section) as an `ARRANGE` line, keys, nonces and inputs as `BYTES`, and a `DIFF` line for every digest, MAC, ciphertext, shared secret or signature compared.
- The test classes are in namespace `Curl.Cryptography` (not `Curl.Cryptography.UnitTests`), which the filter below uses.

## Acceptance criteria

- [ ] `dotnet test Curl.Cryptography.UnitTests --filter "FullyQualifiedName~Curl.Cryptography.MlDsaEncodingTests.|FullyQualifiedName~Curl.Cryptography.MlDsaTests.|FullyQualifiedName~Curl.Cryptography.MlKemTests.|FullyQualifiedName~Curl.Cryptography.MontgomeryModulusTests.|FullyQualifiedName~Curl.Cryptography.Poly1305Tests.|FullyQualifiedName~Curl.Cryptography.Rc4Tests.|FullyQualifiedName~Curl.Cryptography.Ripemd160Tests.|FullyQualifiedName~Curl.Cryptography.RsaCrtPrivateKeyTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [ ] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [ ] `dotnet build Curl.Cryptography.UnitTests -warnaserror` is clean and `dotnet test Curl.Cryptography.UnitTests --filter "TestCategory!=Integration"` passes.
- [ ] The task's commits change only files under `Curl.Cryptography.UnitTests/` and this task file.
- [ ] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

## Log

- 2026-10-07: Created.
