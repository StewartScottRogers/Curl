---
id: BL-1563
title: Make Curl.Cli.UnitTests' CommandLineGssApiOptionTests to CommandLineHttpRequestOptionTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Cli.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1563 — Make Curl.Cli.UnitTests' CommandLineGssApiOptionTests to CommandLineHttpRequestOptionTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Cli.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CommandLineGssApiOptionTests.cs`, `CommandLineHappyEyeballsTimeoutTests.cs`, `CommandLineHelpAndManualOptionTests.cs`, `CommandLineHstsOptionTests.cs`, `CommandLineHttpRequestOptionTests.cs` (81 test methods, counted 2026-10-07).

## Context

- Split from BL-1459 (one per range of files, as BL-1463 and BL-1464 were split); BL-1459 keeps the whole-project checks and depends on this task. Read BL-1459's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR (ADR-0417); add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- What matters here: the full argument array (each argument quoted, so spaces and empty arguments show), the parsed option values the test checks, and for a refusal the exit code and the exact stderr line or lines.
- A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them. Every class in these files is in namespace `Curl.Cli` and named after its file, which the filter below uses.

## Acceptance criteria

- [x] `dotnet test Curl.Cli.UnitTests --filter "FullyQualifiedName~Curl.Cli.CommandLineGssApiOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineHappyEyeballsTimeoutTests.|FullyQualifiedName~Curl.Cli.CommandLineHelpAndManualOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineHstsOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineHttpRequestOptionTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Cli.UnitTests -warnaserror` is clean and `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Cli.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts (Assert. / [TestMethod / [DataRow(), before and after, identical: CommandLineGssApiOptionTests 31/12/18; CommandLineHappyEyeballsTimeoutTests 22/10/14; CommandLineHelpAndManualOptionTests 47/15/47; CommandLineHstsOptionTests 16/7/0; CommandLineHttpRequestOptionTests 66/37/28. `Diagnostics.Assert(` does not match `Assert.`, so the counts are of the real assertions.
- The filtered detailed run on 2026-10-07 ran 163 tests (the 81 methods with their data rows), all passed, each with an `END` line and none with a zero arrange, act or assert count. No test printed a `SLOW:` line; the whole run took under a second.
- Each class wraps `CommandLineParser.Parse` in a private `Parse` that writes `ARRANGE arguments` (each argument quoted) and the `ACT` lines from `CommandLineParseDiagnostics.ActParse`; each test then writes `ASSERT` lines for the values it checks, refusals through a shared helper (exit code and stderr lines). Config files, header files and standard input go through `BYTES`. The two `CommandLineNumber.ParseMilliseconds` tests and the two null-header guard tests write their own ARRANGE and ACT lines.
- `dotnet build Curl.Cli.UnitTests -warnaserror` clean; `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"`: 3770 passed, 16 skipped, 0 failed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. All 163 test cases in the five files write ARRANGE, ACT and ASSERT diagnostics; build clean, fast tests green.
