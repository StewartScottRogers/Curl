---
id: BL-1641
title: Make Curl.Tls.UnitTests' Tls13 ClientStream and EncryptedClientHello tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Tls.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1641 — Make Curl.Tls.UnitTests' Tls13 ClientStream and EncryptedClientHello tests write descriptive diagnostic output

## Goal

Every test in `Curl.Tls.UnitTests`' `Tls13ClientStreamTests` and `Tls13EncryptedClientHelloTests` (2 files, 53 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed.

## Context

- Split from BL-1489 (one task per range of files, as its Notes direct); BL-1489 keeps the whole-project checks and depends on this task. Follow BL-1489's Context for what matters in this project, and `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- Every class is in namespace `Curl.Tls`, flat in the project root. A shared fake or helper (`EchTestFrontEnd`, `Tls13TestServer`) may write lines for the tests that use it, as long as each test's `END` line counts them.

## Acceptance criteria

- [x] `dotnet test Curl.Tls.UnitTests --filter "FullyQualifiedName~Curl.Tls.Tls13ClientStreamTests.|FullyQualifiedName~Curl.Tls.Tls13EncryptedClientHelloTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Tls.UnitTests -warnaserror` is clean and `dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Tls.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts before -> after: `Tls13ClientStreamTests.cs` Assert. 68 -> 68, [TestMethod 26 -> 26, [DataRow( 5 -> 5; `Tls13EncryptedClientHelloTests.cs` Assert. 94 -> 94, [TestMethod 27 -> 27, [DataRow( 6 -> 6. No assertion was removed or changed; the diagnostics sit beside them.
- The filtered detailed run printed 61 `END` lines (53 methods, 61 runs with data rows), none with an arrange, act or assert count of 0.
- `PHASE` timings: `connect` and `transfer` in `ApplicationDataCrossesBothWaysWithEachCipherSuite`; `handshake` in the ECH acceptance, HelloRetryRequest and resumption tests. Shared helpers became instance methods so they write lines: `AssertClientAnswersAsync` (now also takes a description of what the server sends) and the new `ActHandshake`.
- No test printed a `SLOW:` line (slowest about 120 ms), so no performance follow-up was filed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. 53 Tls13ClientStream and Tls13EncryptedClientHello tests write ARRANGE/ACT/ASSERT diagnostics; 61 END lines, none with a zero count; Curl.Tls.UnitTests 1283 fast tests pass
