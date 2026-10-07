---
id: BL-1566
title: Make Curl.Cli.UnitTests' CommandLineNumberTests to CommandLineParallelOptionTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Cli.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1566 — Make Curl.Cli.UnitTests' CommandLineNumberTests to CommandLineParallelOptionTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Cli.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CommandLineNumberTests.cs`, `CommandLineOptionTableTests.cs`, `CommandLineOptionTests.cs`, `CommandLineOutNullOptionTests.cs`, `CommandLineParallelOptionTests.cs` (87 test methods, counted 2026-10-07).

## Context

- Split from BL-1459 (one per range of files, as BL-1463 and BL-1464 were split); BL-1459 keeps the whole-project checks and depends on this task. Read BL-1459's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR (ADR-0417); add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- What matters here: the full argument array (each argument quoted, so spaces and empty arguments show), the parsed option values the test checks, and for a refusal the exit code and the exact stderr line or lines.
- A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them. Every class in these files is in namespace `Curl.Cli` and named after its file, which the filter below uses.

## Acceptance criteria

- [x] `dotnet test Curl.Cli.UnitTests --filter "FullyQualifiedName~Curl.Cli.CommandLineNumberTests.|FullyQualifiedName~Curl.Cli.CommandLineOptionTableTests.|FullyQualifiedName~Curl.Cli.CommandLineOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineOutNullOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineParallelOptionTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Cli.UnitTests -warnaserror` is clean and `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Cli.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Each class wraps the call it tests in a private helper that writes the lines, so no test's logic or assertion changed:
  - `CommandLineNumberTests`: `ParseNonNegative`, `ParseMinusOneOrMore` and `ParseOctal` write the spelled option and value (quoted), the ceiling, whether it refused, the exit code, every stderr line and the number. Each test adds an `ASSERT number` line. The null-argument tests write the call, the exception type and the parameter name.
  - `CommandLineOutNullOptionTests` and `CommandLineParallelOptionTests`: a `Parse` helper writes the quoted argument array (`ArrangeArguments`) and the parse result (`ActParse`). OutNull also writes each URL output and every after-transfer warning; Parallel writes each group's parallel settings. `Accept` and `AssertRefused` write the accepted check and the first stderr line.
  - `CommandLineOptionTableTests` and `CommandLineOptionTests`: write the builder, long and short name, the value and spelled option passed to `Apply`, and the option's properties or the refusal and its stderr lines. Each test adds an `ASSERT` line mirroring its first assertion.
- `PlatformLongMaximum_IsTheCeilingOfThisOperatingSystem` writes only whether the ceilings match, not the ceiling itself, so nothing printed depends on the operating system.
- Counts, before -> after (`Assert.` / `[TestMethod` / `[DataRow(`): the five files are unchanged at Number 59/24/49, OptionTable 12/6/44, Option 85/37/5, OutNull 36/8/2, Parallel 21/12/32. `Diagnostics.Assert(` does not match `Assert.`.
- The filtered detailed run printed 196 `END` lines for 196 tests, none with a zero count. No test printed a `SLOW:` line.
- `Curl.Cli.UnitTests` fast tests: 3770 passed, 16 skipped, 0 failed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every test in CommandLineNumberTests to CommandLineParallelOptionTests writes ARRANGE, ACT and ASSERT diagnostics
