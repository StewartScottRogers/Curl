---
id: BL-1530
title: Make Curl.Core.UnitTests' Multipart tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Core.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1530 — Make Curl.Core.UnitTests' Multipart tests write descriptive diagnostic output

## Goal

Every test in `Curl.Core.UnitTests/Multipart/` (10 files, 99 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs (the parts), Act result and assertion context, with multipart bodies through `BYTES` and `DIFF` (plus `PHASE` timings where it has phases), with no test's logic or assertions changed.

## Context

- Split from BL-1463 (one per range of files, as its Notes direct); BL-1463 keeps the whole-project checks and depends on this task. Read BL-1463's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Core.UnitTests --filter "FullyQualifiedName~Curl.Core.Multipart." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Core.UnitTests -warnaserror` is clean and `dotnet test Curl.Core.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Core.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts across `Curl.Core.UnitTests/Multipart/*.cs`: before `Assert.` 209, `[TestMethod` 99, `[DataRow(` 85; after 209, 99, 85. No assertion or expected value changed; inline asserts on a call's result were hoisted into a local first, same call order.
- The builder tests' shared helpers (`BuildAsync`, `AssertBodyAsync`, `ReadAllAsync`) write the ARRANGE, ACT, BYTES and DIFF lines and a `PHASE build` line, so each test that goes through them is covered.
- Detailed run of `FullyQualifiedName~Curl.Core.Multipart.`: 164 passed, 1 skipped (OS-conditioned), 164 `END` lines, none with a zero count. Whole project fast tests: 1441 passed, 6 skipped.
- `SLOW:` lines: none.
- Part names containing CR or LF (name-escaping tests) print raw in ARRANGE lines; the same on every OS.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every Multipart test writes ARRANGE, ACT and ASSERT/DIFF diagnostics, bodies as BYTES and DIFF
