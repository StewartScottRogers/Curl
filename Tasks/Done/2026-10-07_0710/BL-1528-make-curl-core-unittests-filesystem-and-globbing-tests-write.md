---
id: BL-1528
title: Make Curl.Core.UnitTests' FileSystem and Globbing tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Core.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1528 — Make Curl.Core.UnitTests' FileSystem and Globbing tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Core.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `FileSystem/FileOpenFailureTests.cs`, `FileSystem/PhysicalFileSystemTests.cs`, `Globbing/UrlGlobTests.cs` (82 test methods, counted 2026-10-07).

## Context

- Split from BL-1463 (one per range of files, as its Notes direct); BL-1463 keeps the whole-project checks and depends on this task. Read BL-1463's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system (print paths relative to the test's temporary directory where the full path differs by platform).

## Acceptance criteria

- [x] `dotnet test Curl.Core.UnitTests --filter "FullyQualifiedName~FileOpenFailureTests|FullyQualifiedName~PhysicalFileSystemTests|FullyQualifiedName~UrlGlobTests" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Core.UnitTests -warnaserror` is clean and `dotnet test Curl.Core.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Core.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts before -> after (`Assert.`, `[TestMethod`, `[DataRow(`): FileOpenFailureTests 16/16/3 -> 16/16/3; PhysicalFileSystemTests 63/35/2 -> 63/35/2; UrlGlobTests 76/31/115 -> 76/31/115. Every original assertion is kept inline; each gains a `diagnostics.Assert` or `Diff` line beside it.
- Filtered detailed run: 195 tests, 192 passed and printed an `END` line with nonzero arrange, act and assert counts; 3 skipped by `OSCondition` (POSIX-only) print none. No test printed `SLOW:`, so no follow-up task.
- Choices: paths are printed as the name relative to the test's temporary directory (or "the temporary directory itself"), so nothing printed depends on the platform; control characters in glob inputs and messages print as `\uXXXX` so each value stays on one line; the 40000-character file name and the 100- to 201-copy URLs print as a description, not in full. No phases were added: these tests have no separate stages worth timing.
- `Assert.IsTrue(UrlGlob.TryParse(...))` stays inline (not split into a bool local) so the compiler still knows the `out` value is non-null after it; Act lines follow it.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. FileOpenFailure, PhysicalFileSystem and UrlGlob tests write ARRANGE, ACT and ASSERT/DIFF diagnostics
