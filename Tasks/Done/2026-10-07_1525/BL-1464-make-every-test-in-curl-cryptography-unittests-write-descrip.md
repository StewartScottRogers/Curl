---
id: BL-1464
title: Make every test in Curl.Cryptography.UnitTests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457, BL-1535, BL-1536, BL-1537, BL-1538, BL-1539, BL-1540]
touches: [Curl.Cryptography.UnitTests]
requirement: none
created: 2026-10-04
completed: 2026-10-07
---
# BL-1464 — Make every test in Curl.Cryptography.UnitTests write descriptive diagnostic output

## Goal

Every test in `Curl.Cryptography.UnitTests` writes, through BL-1457's shared `TestDiagnostics` helper, the Arrange inputs that matter, its Act result and its assertion context (expected against actual, first differing byte or character), plus `PHASE` timings where it has distinct phases, so an AI reading a failed or slow test's log can debug it or understand its time without re-running it; no test's logic or assertions change.

## Context

- Stewart's request, plan approved 2026-10-04: every unit test writes enough descriptive console output that an AI reading a failed or slow test's log can debug it without re-running it; slow budget 3 seconds per test. One task per test project; this one is `Curl.Cryptography.UnitTests`, which tests `Curl.Cryptography.UnitLibrary`.
- BL-1457 links the root `TestDiagnostics.cs` into every test project and writes each test's `START`, `END ... (arrange <a>, act <b>, assert <c>)` and `SLOW:` lines itself. The line format (`ARRANGE`, `ACT`, `ASSERT`, `BYTES`, `DIFF`, `PHASE`) and how a test calls the helper are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; follow them and add no prefix of your own.
- Size, counted 2026-10-04 by matching `[TestMethod]`, `[TestMethod(` and `[DataTestMethod]` in the project's `.cs` files: 416 test methods in 50 files, with 1018 `[DataRow(` lines. Solution-wide, 21 test files reference `TestContext` (mostly for its `CancellationToken`) and only 3, all in `Curl.Console.UnitTests`, write any output today.
- What matters here: the test vector's source (RFC and section) as an `ARRANGE` line, keys, nonces and inputs as `BYTES`, and a `DIFF` line for every digest, MAC, ciphertext, shared secret or signature compared.
- Output only. No test method, data row, assertion or arrange step is removed, weakened or changed in what it tests. A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them.
- Keep the log readable: large payloads go through `BYTES` (which caps itself), never a loop printing thousands of lines. Nothing printed may make a test depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Cryptography.UnitTests --filter "TestCategory!=Integration" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs (as many `END` lines as the run's total test count), and piping that output to `Select-String -Pattern 'END .*(\(arrange 0,|, act 0,|, assert 0\))'` prints nothing: every test wrote at least one `ARRANGE`, one `ACT` and one `ASSERT` or `DIFF` line.
- [x] The run's total test count is unchanged, and in `Curl.Cryptography.UnitTests` (excluding `obj`) the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches (each counted with `Select-String -AllMatches`) are no lower than before the task; the before and after numbers are recorded in Notes.
- [x] `dotnet build Curl.Cryptography.UnitTests -warnaserror` is clean and `dotnet test Curl.Cryptography.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Cryptography.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; for each that is a real performance problem a follow-up task is filed and its ID is in Notes.

## Notes

- Sized for one run: 416 test methods in 50 files.
- 2026-10-07 (lane 9): split before changing any test, as BL-1463 was. 416 test methods in 50 files is far more than one run's two hours and token budget. Six tasks cover every test file, in alphabetical groups of 64 to 75 tests: BL-1535 (AEAD, AES, ARIA, bcrypt-pbkdf, Blowfish), BL-1536 (Brainpool, Camellia, CAST-128, ChaCha20, ConstantTime), BL-1537 (DES, DSA, Ed25519, Ed448, Edwards25519, Field25519), BL-1538 (finite-field DH, fixed-block hash, GCM, HMAC-RIPEMD-160, HPKE, MD4), BL-1539 (ML-DSA, ML-KEM, Montgomery, Poly1305, RC4, RIPEMD-160, RSA-CRT), BL-1540 (Scalar25519 to X448). Their filters use namespace `Curl.Cryptography`. Once they are Done, this task runs only the whole-project checks.
- Before counts (2026-10-07, `Select-String -AllMatches` over `Curl.Cryptography.UnitTests` excluding `obj` and `bin`): `Assert.` 756, `[TestMethod`/`[DataTestMethod` 416, `[DataRow(` 1018.
- 2026-10-07 (lane 3), whole-project checks after BL-1535..BL-1540:
  - Detailed fast run: 1335 tests, 1335 passed, 1335 `END` lines; the zero-count `Select-String` prints nothing. `dotnet build Curl.Cryptography.UnitTests -warnaserror`: 0 warnings, 0 errors.
  - After counts: `Assert.` 756, `[TestMethod` 416 (`[DataTestMethod` 0), `[DataRow(` 1018 - all equal to before. Total test count unchanged: `git diff 4ca658719^ HEAD -- Curl.Cryptography.UnitTests` (from just before BL-1535's first commit) adds or removes no `[TestMethod`, `[DataRow`, `[DynamicData`, `[TestCategory`, `[Ignore` or `[OSCondition` line, so the run's 1335 is the before count too (a before-commit worktree run was not permitted in this lane).
  - This task changes only this task file; the test changes are BL-1535..BL-1540's commits, each under `Curl.Cryptography.UnitTests/` and its own task file.
  - `SLOW:` lines (Debug, nine lanes building), with `PHASE` breakdowns:
    - `BcryptPbkdfTests.DeriveKey_OneRoundMaximumLengthKey_FillsEveryByte` 20251 ms (`derive` 20250), `DeriveKey_GoGoldenVector2_GivesThePublishedKey` 15513 ms (`derive` 15513), `DeriveKey_GoGoldenVector0_GivesThePublishedKey` 8341 ms (`derive` 8340): real, already filed as BL-1558.
    - `X448Tests.TryComputeSharedSecret_Rfc7748Section52Iterations_GiveTheExpectedK` (1000-iteration row) 7885 ms (`iterate` 7885): real, already filed as BL-1645.
    - `BrainpoolEcdsaTests.VerifyHash_EveryWycheproofP256r1Vector_GivesItsExpectedResult` 3824 ms (`verify every vector` 3824), `DsaSignatureTests.VerifyHash_CavpSigVer_GivesPublishedResult` largest row 3110 ms (`verify` 3110), `FiniteFieldDiffieHellmanTests.Generate_TwoParties_AgreeOnTheSharedSecret` 3894 and 4038 ms and `ComputePublicValueAndSharedSecret_FixedExponents_EqualBigIntegerModPow` 3147 ms (`public values and shared secrets` at most 1222 ms; the rest is outside the phase). All stayed over 3 s when re-run alone, so filed as BL-1646.

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Split into BL-1535..BL-1540 (one per range of test files); waits on them, then runs only the whole-project checks
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every one of Curl.Cryptography.UnitTests' 1335 tests writes ARRANGE, ACT and ASSERT/DIFF diagnostics; counts unchanged
