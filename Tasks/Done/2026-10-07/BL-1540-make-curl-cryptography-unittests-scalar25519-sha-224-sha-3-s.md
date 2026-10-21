---
id: BL-1540
title: Make Curl.Cryptography.UnitTests' Scalar25519, SHA-224, SHA-3, SHAKE, sntrup761, sorting network, Uint14, X25519 and X448 tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Cryptography.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1540 — Make Curl.Cryptography.UnitTests' Scalar25519, SHA-224, SHA-3, SHAKE, sntrup761, sorting network, Uint14, X25519 and X448 tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Cryptography.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `Scalar25519Tests.cs`, `Sha224Tests.cs`, `Sha3Tests.cs`, `ShakeTests.cs`, `Sntrup761EncodingTests.cs`, `Sntrup761Tests.cs`, `SortingNetworkTests.cs`, `Uint14DivisionTests.cs`, `X25519Tests.cs`, `X448Tests.cs` (64 test methods, counted 2026-10-07).

## Context

- Split from BL-1464 (one per range of files, as BL-1463 was split); BL-1464 keeps the whole-project checks and depends on this task. Read BL-1464's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR (ADR-0417); add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- What matters here: the test vector's source (RFC and section) as an `ARRANGE` line, keys, nonces and inputs as `BYTES`, and a `DIFF` line for every digest, MAC, ciphertext, shared secret or signature compared.
- The test classes are in namespace `Curl.Cryptography` (not `Curl.Cryptography.UnitTests`), which the filter below uses.

## Acceptance criteria

- [x] `dotnet test Curl.Cryptography.UnitTests --filter "FullyQualifiedName~Curl.Cryptography.Scalar25519Tests.|FullyQualifiedName~Curl.Cryptography.Sha224Tests.|FullyQualifiedName~Curl.Cryptography.Sha3Tests.|FullyQualifiedName~Curl.Cryptography.ShakeTests.|FullyQualifiedName~Curl.Cryptography.Sntrup761EncodingTests.|FullyQualifiedName~Curl.Cryptography.Sntrup761Tests.|FullyQualifiedName~Curl.Cryptography.SortingNetworkTests.|FullyQualifiedName~Curl.Cryptography.Uint14DivisionTests.|FullyQualifiedName~Curl.Cryptography.X25519Tests.|FullyQualifiedName~Curl.Cryptography.X448Tests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Cryptography.UnitTests -warnaserror` is clean and `dotnet test Curl.Cryptography.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Cryptography.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts in the ten files, before -> after: `Assert.` 99 -> 99, `[TestMethod` 64 -> 64, `[DataRow(` 105 -> 105. No assertion changed; where a test asserted on an inline call (`Assert.IsTrue(X25519.TryComputeSharedSecret(...))`, `Assert.AreEqual(expected, Iterate(n))`) the call now lands in a local first so its result can be written as `ACT`.
- The acceptance filter as written also selects the two `[TestCategory("Integration")]` million-iteration RFC 7748 tests in `X25519Tests` and `X448Tests` (minutes and hours); the first run was stopped after an hour because of them. The check was run with `&TestCategory!=Integration` added: 135 tests, 135 `END` lines, none with a zero arrange, act or assert count. The two Integration tests carry the same diagnostics (`ARRANGE` source and iterations, `PHASE iterate`, `ACT` and `DIFF` of k).
- `BYTES` lines are not counted on `END`, so every test that shows its result as `BYTES` also writes an `ACT` line (hex or a length).
- `SLOW:` lines: `X25519Tests.TryComputeSharedSecret_Rfc7748Section52Iterations_GiveTheExpectedK` (1,000 iterations, 3238 ms, `PHASE iterate: 3237 ms`) and `X448Tests.TryComputeSharedSecret_Rfc7748Section52Iterations_GiveTheExpectedK` (1,000 iterations, 14726 ms, `PHASE iterate: 14726 ms`). Both are the ladders themselves, about 3 ms per X25519 and 15 ms per X448 scalar multiplication; the follow-up is the existing BL-1525 (make X25519, X448 and CAST-128 fast enough). sntrup761 key generation takes 209-255 ms per known answer (`PHASE key generation`), within budget.
- `dotnet build Curl.Cryptography.UnitTests -warnaserror` clean; `dotnet test Curl.Cryptography.UnitTests --filter "TestCategory!=Integration"`: 1335 passed; `dotnet format --verify-no-changes` clean on the ten files.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Scalar25519, SHA-224, SHA-3, SHAKE, sntrup761, sorting network, Uint14, X25519 and X448 tests write ARRANGE, ACT, ASSERT/DIFF and PHASE diagnostics
