---
id: BL-1537
title: Make Curl.Cryptography.UnitTests' DES, DSA, Ed25519, Ed448, Edwards25519 and Field25519 tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Cryptography.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1537 — Make Curl.Cryptography.UnitTests' DES, DSA, Ed25519, Ed448, Edwards25519 and Field25519 tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Cryptography.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `DesTests.cs`, `DeterministicDsaNonceTests.cs`, `DsaSignatureTests.cs`, `Ed25519Tests.cs`, `Ed448Tests.cs`, `Edwards25519Tests.cs`, `Field25519Tests.cs` (69 test methods, counted 2026-10-07).

## Context

- Split from BL-1464 (one per range of files, as BL-1463 was split); BL-1464 keeps the whole-project checks and depends on this task. Read BL-1464's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR (ADR-0417); add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- What matters here: the test vector's source (RFC and section) as an `ARRANGE` line, keys, nonces and inputs as `BYTES`, and a `DIFF` line for every digest, MAC, ciphertext, shared secret or signature compared.
- The test classes are in namespace `Curl.Cryptography` (not `Curl.Cryptography.UnitTests`), which the filter below uses.

## Acceptance criteria

- [x] `dotnet test Curl.Cryptography.UnitTests --filter "FullyQualifiedName~Curl.Cryptography.DesTests.|FullyQualifiedName~Curl.Cryptography.DeterministicDsaNonceTests.|FullyQualifiedName~Curl.Cryptography.DsaSignatureTests.|FullyQualifiedName~Curl.Cryptography.Ed25519Tests.|FullyQualifiedName~Curl.Cryptography.Ed448Tests.|FullyQualifiedName~Curl.Cryptography.Edwards25519Tests.|FullyQualifiedName~Curl.Cryptography.Field25519Tests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Cryptography.UnitTests -warnaserror` is clean and `dotnet test Curl.Cryptography.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Cryptography.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- All 69 test methods in the seven files write their diagnostics through `TestDiagnostics`: the vector's source (NIST SP 500-20, Grabbe, FIPS 74, RFC 6979 A.2.1/A.2.2, NIST CAVP FIPS 186-4 SigVer, FIPS 180-2 appendix B, RFC 8032 sections 5.1.3, 5.1.7, 5.2.3, 5.2.7, 7.1 and 7.4, RFC 7748) as `ARRANGE`, keys, messages, contexts and signatures as `BYTES`, and a `DIFF` for every ciphertext, k, digest, public key, signature and field encoding compared. Throw tests capture the exception and `ASSERT` its type; boolean results (`Verify`, `TryDecode`) get an `ASSERT`. `PHASE` timings wrap the CAVP SigVer loop and the 64-trial BCL comparison; inside loops, `BYTES`/`DIFF` are written only for a failing vector so passing runs stay short. The RFC 6979 signing helper became an instance method taking the vector source so it can reach `TestContext`.
- Acceptance filter run: 199 tests, 199 `END` lines, none with a zero count.
- Counts (`Assert.` / `[TestMethod` / `[DataRow(`), before and after, unchanged: Des 7/6/9, DeterministicDsaNonce 2/2/7, DsaSignature 19/17/50, Ed25519 17/12/19, Ed448 21/14/26, Edwards25519 10/6/5, Field25519 13/12/14; total 89/69/130.
- No test printed a `SLOW:` line. The slowest is `DsaSignatureTests.VerifyHash_CavpSigVer_GivesPublishedResult` for L=3072 at 2267 ms (`PHASE verify: 2264 ms`, 15 vectors of 3072-bit modular exponentiation), under the 3000 ms budget; the L=2048 rows took 1013 and 911 ms. No follow-up filed.
- `dotnet build Curl.Cryptography.UnitTests -warnaserror` clean; `dotnet test Curl.Cryptography.UnitTests --filter "TestCategory!=Integration"`: 1335 passed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. DES, DSA, Ed25519, Ed448, Edwards25519 and Field25519 tests write ARRANGE, ACT, ASSERT/DIFF and PHASE diagnostics
