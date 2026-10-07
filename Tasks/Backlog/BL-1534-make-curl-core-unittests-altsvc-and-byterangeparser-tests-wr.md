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
completed:
---
# BL-1534 — Make Curl.Core.UnitTests' AltSvc and ByteRangeParser tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Core.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `AltSvc/AltSvcAlpnTokenTests.cs`, `AltSvc/AltSvcCacheDiagnosticLogTests.cs`, `AltSvc/AltSvcCacheTests.cs`, `AltSvc/AltSvcFileLineParserTests.cs`, `AltSvc/AltSvcHeaderParserTests.cs`, `ByteRangeParserTests.cs` (72 test methods, counted 2026-10-07).

## Context

- Split from BL-1463 (one per range of files, as its Notes direct); BL-1463 keeps the whole-project checks and depends on this task. Read BL-1463's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.

## Acceptance criteria

- [ ] `dotnet test Curl.Core.UnitTests --filter "FullyQualifiedName~Curl.Core.AltSvc.|FullyQualifiedName~ByteRangeParserTests" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [ ] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [ ] `dotnet build Curl.Core.UnitTests -warnaserror` is clean and `dotnet test Curl.Core.UnitTests --filter "TestCategory!=Integration"` passes.
- [ ] The task's commits change only files under `Curl.Core.UnitTests/` and this task file.
- [ ] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

## Log

- 2026-10-07: Created.
