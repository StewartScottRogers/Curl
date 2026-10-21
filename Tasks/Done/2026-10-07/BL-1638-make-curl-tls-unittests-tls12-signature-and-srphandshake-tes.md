---
id: BL-1638
title: Make Curl.Tls.UnitTests' Tls12 Signature and SrpHandshake tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Tls.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1638 — Make Curl.Tls.UnitTests' Tls12 Signature and SrpHandshake tests write descriptive diagnostic output

## Goal

Every test in `Curl.Tls.UnitTests`' `Tls12SignatureTests` and `Tls12SrpHandshakeTests` (2 files, 43 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed.

## Context

- Split from BL-1489 (one task per range of files, as its Notes direct); BL-1489 keeps the whole-project checks and depends on this task. Follow BL-1489's Context for what matters in this project, and `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- Every class is in namespace `Curl.Tls`, flat in the project root. A shared fake or helper may write lines for the tests that use it, as long as each test's `END` line counts them.

## Acceptance criteria

- [x] `dotnet test Curl.Tls.UnitTests --filter "FullyQualifiedName~Curl.Tls.Tls12SignatureTests.|FullyQualifiedName~Curl.Tls.Tls12SrpHandshakeTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Tls.UnitTests -warnaserror` is clean and `dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Tls.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts of `Assert.` / `[TestMethod` / `[DataRow(` matches, before -> after: Tls12SignatureTests.cs 73/27/24 -> 73/27/24; Tls12SrpHandshakeTests.cs 23/16/15 -> 23/16/15. Where a test asserted on a call result inline, the call now runs once into a local (in Act) and the same assertion checks the local; no assertion was removed or weakened.
- Handshake tests pass `Diagnostics` to `Tls12HandshakeDriver.Run`, which writes a PHASE line per flight. The SRP class's shared helpers (`AssertCompletesWithServerKeys`, `AssertServerKeyExchangeFails`) became instance methods so they write through the test's diagnostics; their lines count in that test's `END`.
- Filtered run: 73 tests, 73 `END` lines, none with a zero count. SLOW: none (every test under 3 s; the filtered run took about 1.7 s).
- No library changed, so no Measure-CodeQuality run was needed (coverage is of `*.UnitLibrary` code, which this task does not touch).

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Tls12Signature and Tls12SrpHandshake tests write ARRANGE/ACT/ASSERT diagnostics (PHASE per handshake flight); 73/73 END lines, no assertion changed
