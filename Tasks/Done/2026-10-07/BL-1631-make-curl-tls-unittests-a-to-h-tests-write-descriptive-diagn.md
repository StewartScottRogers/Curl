---
id: BL-1631
title: Make Curl.Tls.UnitTests' A to H tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Tls.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1631 — Make Curl.Tls.UnitTests' A to H tests write descriptive diagnostic output

## Goal

Every test in `Curl.Tls.UnitTests`' `ClientHelloProfileTests`, `CompressedCertificateTests`, `EchConfigListTests`, `HandshakeMessageReaderTests`, `HandshakeMessageRejectionTests` and `HkdfLabelTests` (6 files, 55 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed.

## Context

- Split from BL-1489 (one task per range of files, as its Notes direct); BL-1489 keeps the whole-project checks and depends on this task. Follow BL-1489's Context: what matters in this project (records and handshake messages as `BYTES` with their decoded type, negotiated version, cipher suite and group, secrets and transcript hashes as hex with a `DIFF` against RFC 8448 values, alerts with their codes, `PHASE` per handshake flight), `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- Every class is in namespace `Curl.Tls`, flat in the project root. A shared fake or helper may write lines for the tests that use it, as long as each test's `END` line counts them.

## Acceptance criteria

- [x] `dotnet test Curl.Tls.UnitTests --filter "FullyQualifiedName~Curl.Tls.ClientHelloProfileTests.|FullyQualifiedName~Curl.Tls.CompressedCertificateTests.|FullyQualifiedName~Curl.Tls.EchConfigListTests.|FullyQualifiedName~Curl.Tls.HandshakeMessageReaderTests.|FullyQualifiedName~Curl.Tls.HandshakeMessageRejectionTests.|FullyQualifiedName~Curl.Tls.HkdfLabelTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Tls.UnitTests -warnaserror` is clean and `dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Tls.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts (`Assert.` / `[TestMethod` / `[DataRow(`), before and after, unchanged in every file:
  ClientHelloProfileTests 16/9/0, CompressedCertificateTests 12/9/18, EchConfigListTests 32/14/21,
  HandshakeMessageReaderTests 14/4/7, HandshakeMessageRejectionTests 13/8/0, HkdfLabelTests 10/11/8.
- The filtered detailed run printed 111 `END` lines for 111 tests (data rows and dynamic data
  expanded), none with a zero arrange, act or assert count.
- No test printed a `SLOW:` line; every one ran in a few milliseconds, so none has phases.
- Choices: alerts print as `<name> (<code>)`; group, algorithm and extension codes as `0x%04x`;
  records, flights and bodies through `BYTES`, and re-encodings and RFC values through `DIFF`.
  The truncation loop in `DecodeAnswersATruncatedBodyWithDecodeError` writes one summary
  `ASSERT` (lengths answered with decode_error against body length) rather than a line per
  length, to keep the log readable. Exception tests keep `Assert.ThrowsExactly` inline and
  write the exception's message or parameter name as the `ACT` line.
- Full fast run of `Curl.Tls.UnitTests`: 1283 passed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Curl.Tls.UnitTests' six A to H test classes write ARRANGE, ACT and ASSERT/DIFF diagnostics for all 111 tests
