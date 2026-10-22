---
id: BL-1561
title: Make Curl.Cli.UnitTests' CommandLineDohOptionTests to CommandLineFormEscapeTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Cli.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1561 — Make Curl.Cli.UnitTests' CommandLineDohOptionTests to CommandLineFormEscapeTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Cli.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CommandLineDohOptionTests.cs`, `CommandLineDumpHeaderTests.cs`, `CommandLineEtagOptionTests.cs`, `CommandLineExpandOptionTests.cs`, `CommandLineExpect100TimeoutOptionTests.cs`, `CommandLineFastOpenAndMultipathTests.cs`, `CommandLineFormEscapeTests.cs` (69 test methods, counted 2026-10-07).

## Context

- Split from BL-1459 (one per range of files, as BL-1463 and BL-1464 were split); BL-1459 keeps the whole-project checks and depends on this task. Read BL-1459's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR (ADR-0417); add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- What matters here: the full argument array (each argument quoted, so spaces and empty arguments show), the parsed option values the test checks, and for a refusal the exit code and the exact stderr line or lines.
- A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them. Every class in these files is in namespace `Curl.Cli` and named after its file, which the filter below uses.

## Acceptance criteria

- [x] `dotnet test Curl.Cli.UnitTests --filter "FullyQualifiedName~Curl.Cli.CommandLineDohOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineDumpHeaderTests.|FullyQualifiedName~Curl.Cli.CommandLineEtagOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineExpandOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineExpect100TimeoutOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineFastOpenAndMultipathTests.|FullyQualifiedName~Curl.Cli.CommandLineFormEscapeTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Cli.UnitTests -warnaserror` is clean and `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Cli.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Approach (as BL-1560): each class gets a `TestContext` and an instance `Parse` wrapper that writes `ARRANGE arguments` (each argument quoted) and the parse result through `CommandLineParseDiagnostics.ActParse`; refusal helpers write `ASSERT` lines for accepted, exit code and the full stderr lines; accepting tests write `ASSERT` lines for each option value they check, post data through `DIFF` and file content through `BYTES`. No assertion changed what it checks; in `CommandLineDohOptionTests` a few one-line assertions now read the parsed value into a local first so the same value is written and asserted.
- Counts before -> after (`Assert.`, `[TestMethod`, `[DataRow(`): Doh 19/10/3 -> 19/10/3; DumpHeader 16/8/6 -> 16/8/6; Etag 27/16/8 -> 27/16/8; Expand 34/22/62 -> 34/22/62; Expect100Timeout 12/5/9 -> 12/5/9; FastOpenAndMultipath 12/4/0 -> 12/4/0; FormEscape 9/4/4 -> 9/4/4.
- The filtered detailed run printed 142 `END` lines for 142 tests (all passed), none with a zero arrange, act or assert count.
- No test printed a `SLOW:` line.
- `dotnet build Curl.Cli.UnitTests -warnaserror` clean; `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"`: 3770 passed, 16 skipped, 0 failed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every test from CommandLineDohOptionTests to CommandLineFormEscapeTests writes ARRANGE, ACT and ASSERT diagnostics
