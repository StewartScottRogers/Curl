---
id: BL-1555
title: Make Curl.Networking.UnitTests' HandBuiltTlsProviderTests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1555 — Make Curl.Networking.UnitTests' HandBuiltTlsProviderTests write descriptive diagnostic output

## Goal

Every test in `Curl.Networking.UnitTests`' partial class `HandBuiltTlsProviderTests` (`HandBuiltTlsProviderTests.cs` and its 13 `HandBuiltTlsProviderTests.*.cs` files: 14 files, 138 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs (TLS settings, certificates by subject and thumbprint, handshake bytes through `BYTES`), Act result and assertion context (plus `PHASE` timings for the TLS handshake), with no test's logic or assertions changed.

## Context

- Split from BL-1468 (one task per range of files, as its Notes direct); BL-1468 keeps the whole-project checks and depends on this task. Follow BL-1468's Context: what matters in this project, `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Networking.UnitTests --filter "FullyQualifiedName~Curl.Networking.HandBuiltTlsProviderTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Networking.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Shared helpers in `HandBuiltTlsProviderTests.cs`: `TestContext`, `Diagnostics`, `ArrangeCertificate` (subject and thumbprint, never a path) and `ActConnectResult` (exit code, error message, peer certificate count, ALPN); every handshake runs inside `Diagnostics.Phase("TLS handshake")`. `EarlyData` and `ReusedSession` gained small instance helpers for their case records.
- Counts in the 14 files, before -> after: `Assert.` 412 -> 412, `[TestMethod` 138 -> 138, `[DataRow(` 190 -> 190 (`Diagnostics.Assert(` lines are not counted).
- `dotnet test ... --filter FullyQualifiedName~Curl.Networking.HandBuiltTlsProviderTests.` ran 304 tests, all passed, 304 `END` lines, none with a zero arrange, act or assert count.
- No test printed a `SLOW:` line, so no follow-up task.
- `SessionIdAndBeast.NoSessionId_OffersNoSessionAndLeavesTheCacheAlone` now holds `sessions.Take(...)` in a local before asserting on it, because `Take` removes the session and writing it twice would change the result; the assertion checks the same thing.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every HandBuiltTlsProviderTests test writes ARRANGE, ACT, ASSERT and a TLS handshake PHASE through TestDiagnostics
