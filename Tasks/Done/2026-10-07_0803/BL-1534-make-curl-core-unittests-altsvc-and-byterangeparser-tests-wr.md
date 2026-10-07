---
id: BL-1534
title: Make Curl.Core.UnitTests' AltSvc and ByteRangeParser tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Core.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1534 — Make Curl.Core.UnitTests' AltSvc and ByteRangeParser tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Core.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `AltSvc/AltSvcAlpnTokenTests.cs`, `AltSvc/AltSvcCacheDiagnosticLogTests.cs`, `AltSvc/AltSvcCacheTests.cs`, `AltSvc/AltSvcFileLineParserTests.cs`, `AltSvc/AltSvcHeaderParserTests.cs`, `ByteRangeParserTests.cs` (72 test methods, counted 2026-10-07).

## Context

- Split from BL-1463 (one per range of files, as its Notes direct); BL-1463 keeps the whole-project checks and depends on this task. Read BL-1463's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Core.UnitTests --filter "FullyQualifiedName~Curl.Core.AltSvc.|FullyQualifiedName~ByteRangeParserTests" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Core.UnitTests -warnaserror` is clean and `dotnet test Curl.Core.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Core.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Every test in the six files now writes ARRANGE, ACT and ASSERT (or DIFF) lines through `TestDiagnostics.For(TestContext)`; per-file helpers (`Parse`, `Apply`, `Read`, `FormatFile`, `SkipReason`, `AssertAlternatives`, `WriteThrown`) write the lines and return the value the unchanged assertion checks. No assertion was removed and no expected value changed.
- Choice: text with CR, LF or tab is printed escaped (`\r`, `\n`, `\t`) and quoted, so a line's ending is visible; the alt-svc files `ReadFile` is given also go out as a `BYTES` line. Host names longer than 40 characters (the 2048- and 254-character limit cases) print as their length and first 8 characters, and `Alt-Svc` values over 80 characters print truncated beside a `value length` line, so no ARRANGE line carries kilobytes.
- Counts in the six files, before -> after: `Assert.` 86 -> 86 (per file 3, 6, 42, 9, 13, 13, unchanged), `[TestMethod` 72 -> 72, `[DataRow(` 106 -> 106.
- The acceptance filter ran 160 tests (data rows included): 160 `END` lines, none with a zero arrange, act or assert count. `Curl.Core.UnitTests` fast run: 1441 passed, 6 skipped, 0 failed.
- No test printed a `SLOW:` line (the slowest took tens of milliseconds), so no performance follow-up was filed.
- Not run: `Measure-CodeQuality.ps1`, since only a test project changed and no `*.UnitLibrary` code moved.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Curl.Core.UnitTests' AltSvc and ByteRangeParser tests write ARRANGE, ACT and ASSERT diagnostics, assertions unchanged
