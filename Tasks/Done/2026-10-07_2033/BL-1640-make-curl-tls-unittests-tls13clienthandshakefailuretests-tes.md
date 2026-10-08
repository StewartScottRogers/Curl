---
id: BL-1640
title: Make Curl.Tls.UnitTests' Tls13ClientHandshakeFailureTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Tls.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1640 — Make Curl.Tls.UnitTests' Tls13ClientHandshakeFailureTests tests write descriptive diagnostic output

## Goal

Every test in `Curl.Tls.UnitTests`' `Tls13ClientHandshakeFailureTests` (1 file, 62 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed.

## Context

- Split from BL-1489 (one task per range of files, as its Notes direct); BL-1489 keeps the whole-project checks and depends on this task. Follow BL-1489's Context for what matters in this project - here above all the alert sent or received with its code, and the flight the handshake failed in - and `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- The class is in namespace `Curl.Tls`, flat in the project root. A shared fake or helper (`Tls13TestServer`, `HandshakeDriver`) may write lines for the tests that use it, as long as each test's `END` line counts them.

## Acceptance criteria

- [x] `dotnet test Curl.Tls.UnitTests --filter "FullyQualifiedName~Curl.Tls.Tls13ClientHandshakeFailureTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In this file the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Tls.UnitTests -warnaserror` is clean and `dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Tls.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts in `Tls13ClientHandshakeFailureTests.cs`, before -> after: `Assert.` 21 -> 21, `[TestMethod` 62 -> 62, `[DataRow(` 11 -> 11. No assertion or test logic changed; the three `Assert.ThrowsExactly` tests now keep the returned exception to write it.
- The filtered detailed run printed 70 `END` lines (62 methods, data rows expanded), none with a zero arrange, act or assert count; 1283 fast tests in the project pass.
- Every alert test goes through `AssertFails`, which writes the alert and origin (`ACT`) and the alert, `IsComplete` and bytes-to-send checks (`ASSERT`); each test or its private helper writes an `ARRANGE failing flight` line naming the flight the handshake failed in, and `BYTES` for the malformed message. Tests that drive a whole handshake run it inside a `handshake` `PHASE` (`RunTimed`).
- No test printed a `SLOW:` line, so no performance follow-up was filed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every Tls13ClientHandshakeFailureTests test writes ARRANGE, ACT and ASSERT diagnostics; 70 END lines, none zero; project fast tests green
