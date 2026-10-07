---
id: BL-1632
title: Make Curl.Tls.UnitTests' K to O tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Tls.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1632 — Make Curl.Tls.UnitTests' K to O tests write descriptive diagnostic output

## Goal

Every test in `Curl.Tls.UnitTests`' `KeyShareKnownAnswerTests`, `KeyShareTests`, `OcspStapleVerifierTests`, `OcspStaplingHandshakeTests` and `OpenSslCertificateVerifyKnownAnswerTests` (5 files, 68 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed.

## Context

- Split from BL-1489 (one task per range of files, as its Notes direct); BL-1489 keeps the whole-project checks and depends on this task. Follow BL-1489's Context for what matters in this project, and `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- Every class is in namespace `Curl.Tls`, flat in the project root. A shared fake or helper may write lines for the tests that use it, as long as each test's `END` line counts them.

## Acceptance criteria

- [x] `dotnet test Curl.Tls.UnitTests --filter "FullyQualifiedName~Curl.Tls.KeyShareKnownAnswerTests.|FullyQualifiedName~Curl.Tls.KeyShareTests.|FullyQualifiedName~Curl.Tls.OcspStapleVerifierTests.|FullyQualifiedName~Curl.Tls.OcspStaplingHandshakeTests.|FullyQualifiedName~Curl.Tls.OpenSslCertificateVerifyKnownAnswerTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Tls.UnitTests -warnaserror` is clean and `dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Tls.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts before -> after (`Assert.`, `[TestMethod`, `[DataRow(`): KeyShareKnownAnswerTests 35/8/16 -> 35/8/16; KeyShareTests 41/17/47 -> 41/17/47; OcspStapleVerifierTests 44/34/19 -> 44/34/19; OcspStaplingHandshakeTests 15/6/0 -> 15/6/0; OpenSslCertificateVerifyKnownAnswerTests 5/3/10 -> 5/3/10.
- The filtered run prints 155 `END` lines, all `Passed`, none with a zero count. The whole project passes, 1283 tests.
- Shared helpers write lines for the tests that use them, as the Context allows: `OcspStapleVerifierTests.VerifyBytes` writes the chain size, the response as `BYTES` and the outcome as `ACT`; `OcspStaplingHandshakeTests.WriteStaple`, `WriteHandshake` and `AssertOutcome` write the staple, the handshake result and the expected status; `WriteThrown`/`WriteRefused` write an exception or a refused secret. Where an assertion called the code under test inline, the result now goes into a local first, so the same value is printed and asserted; no expected value or assertion kind changed.
- `SLOW:` lines: only `Curl.Tls.KeyShareTests.TwoSharesOnAGroupAgreeOnTheSharedSecret`, for ffdhe6144 (0x0103) and ffdhe8192 (0x0104), and how often depends on the machine's load. Runs on the lane machine: ffdhe6144 `PHASE key generation: 1777 ms`, `PHASE agreement: 1862 ms`, 4678 ms in all; ffdhe8192 `PHASE key generation: 4142 ms`, `PHASE agreement: 2556 ms`, 7639 ms in all, and on the last run `1918 ms` and `1352 ms`, 3926 ms in all. That is one 8192-bit modular exponentiation taking about 1 to 2 s in `Curl.Cryptography.UnitLibrary`'s constant-time `FiniteFieldDiffieHellman`, a real performance problem for `--curves ffdhe8192`: filed as BL-1646.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Curl.Tls.UnitTests' K to O tests (155 runs) write ARRANGE, ACT and ASSERT or DIFF lines; ffdhe8192 slowness filed as BL-1646
