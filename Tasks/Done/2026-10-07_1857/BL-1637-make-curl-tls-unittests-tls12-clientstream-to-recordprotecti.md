---
id: BL-1637
title: Make Curl.Tls.UnitTests' Tls12 ClientStream to RecordProtection tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Tls.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1637 — Make Curl.Tls.UnitTests' Tls12 ClientStream to RecordProtection tests write descriptive diagnostic output

## Goal

Every test in `Curl.Tls.UnitTests`' `Tls12ClientStreamTests`, `Tls12KeyBlockTests`, `Tls12MessageCodecTests`, `Tls12RecordProtectionParametersTests` and `Tls12RecordProtectionTests` (5 files, 53 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed.

## Context

- Split from BL-1489 (one task per range of files, as its Notes direct); BL-1489 keeps the whole-project checks and depends on this task. Follow BL-1489's Context for what matters in this project, and `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- Every class is in namespace `Curl.Tls`, flat in the project root. A shared fake or helper may write lines for the tests that use it, as long as each test's `END` line counts them.

## Acceptance criteria

- [x] `dotnet test Curl.Tls.UnitTests --filter "FullyQualifiedName~Curl.Tls.Tls12ClientStreamTests.|FullyQualifiedName~Curl.Tls.Tls12KeyBlockTests.|FullyQualifiedName~Curl.Tls.Tls12MessageCodecTests.|FullyQualifiedName~Curl.Tls.Tls12RecordProtectionParametersTests.|FullyQualifiedName~Curl.Tls.Tls12RecordProtectionTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Tls.UnitTests -warnaserror` is clean and `dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Tls.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Each class gets `TestContext` and a `Diagnostics` property (`TestDiagnostics.For`), as
  BL-1636 did in `Tls12ClientHandshakeTests`. `Tls12ClientStreamTests` connects through a
  private `ConnectWithDiagnosticsAsync` that writes the connection's `ARRANGE` line and a
  `PHASE handshake`; its shared `AssertClientAnswersAsync` became an instance method that
  writes the record the server sent, the failure and the alert asserted.
  `Tls12RecordProtectionTests.RecordsRoundTripThroughTheReadState` times its writes in
  `PHASE protect and unprotect every write`. Where a test only had `Assert.ThrowsExactly`,
  the returned exception's message is its `ACT` line. Some results were pulled into a
  local before the asserts so they could be printed; no assertion or expected value changed.
- Counts (`Assert.` / `[TestMethod` / `[DataRow(`), before -> after:
  Tls12ClientStreamTests 50/17/0 -> 50/17/0; Tls12KeyBlockTests 17/5/9 -> 17/5/9;
  Tls12MessageCodecTests 32/11/7 -> 32/11/7; Tls12RecordProtectionParametersTests
  10/3/10 -> 10/3/10; Tls12RecordProtectionTests 27/17/9 -> 27/17/9.
- The filtered run printed 167 `END` lines for 167 test runs (data and dynamic rows
  included), none with a zero count. Whole project: 1283 passed.
- No test printed a `SLOW:` line. The slowest were Tls12ClientStreamTests at about
  990 ms each (handshake over the pipe, with other lanes building); the longest
  `PHASE protect and unprotect every write` was 776 ms.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. The 53 Tls12 ClientStream to RecordProtection tests write ARRANGE, ACT and ASSERT diagnostics, with handshake and round-trip PHASE timings
