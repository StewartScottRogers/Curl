---
id: BL-1559
title: Make Curl.Cli.UnitTests' AccountHomeDirectoryTests to CommandLineContinueAtNoClobberTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Cli.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1559 — Make Curl.Cli.UnitTests' AccountHomeDirectoryTests to CommandLineContinueAtNoClobberTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Cli.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `AccountHomeDirectoryTests.cs`, `CommandLineAiHelpOptionTests.cs`, `CommandLineAltSvcOptionTests.cs`, `CommandLineAsciiCrlfAndAppendOptionTests.cs`, `CommandLineAuthAndProxyOptionTests.cs`, `CommandLineClobberTests.cs`, `CommandLineConfigFileTests.cs`, `CommandLineConnectionSwitchTests.cs`, `CommandLineContinueAtNoClobberTests.cs` (94 test methods, counted 2026-10-07).

## Context

- Split from BL-1459 (one per range of files, as BL-1463 and BL-1464 were split); BL-1459 keeps the whole-project checks and depends on this task. Read BL-1459's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR (ADR-0417); add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- What matters here: the full argument array (each argument quoted, so spaces and empty arguments show), the parsed option values the test checks, and for a refusal the exit code and the exact stderr line or lines.
- A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them. Every class in these files is in namespace `Curl.Cli` and named after its file, which the filter below uses.

## Acceptance criteria

- [x] `dotnet test Curl.Cli.UnitTests --filter "FullyQualifiedName~Curl.Cli.AccountHomeDirectoryTests.|FullyQualifiedName~Curl.Cli.CommandLineAiHelpOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineAltSvcOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineAsciiCrlfAndAppendOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineAuthAndProxyOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineClobberTests.|FullyQualifiedName~Curl.Cli.CommandLineConfigFileTests.|FullyQualifiedName~Curl.Cli.CommandLineConnectionSwitchTests.|FullyQualifiedName~Curl.Cli.CommandLineContinueAtNoClobberTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Cli.UnitTests -warnaserror` is clean and `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Cli.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- New shared helper `Curl.Cli.UnitTests/CommandLineParseDiagnostics.cs`: `ArrangeArguments` writes the argument
  array with every argument in double quotes (so empty and spaced arguments show), and `ActParse` writes
  accepted or refused, the refusal's exit code (number and name), every stderr line and every warning line.
  Each file's own private `Parse` wrapper (or the test itself) calls them; `-K` file contents go out as `BYTES`.
- Every test also writes `ASSERT` lines for the values it checks, before the matching `Assert.` call; refusals
  in `CommandLineAuthAndProxyOptionTests` and `CommandLineContinueAtNoClobberTests` also write a `DIFF stderr`.
  No assertion was removed or changed (one `Assert.ThrowsExactly` now keeps its returned exception for an `ACT` line).
- Counts in the nine files, before -> after: `Assert.` 215 -> 215, `[TestMethod` 94 -> 94, `[DataRow(` 169 -> 169.
- The acceptance filter ran 233 tests (232 passed, 1 skipped on Windows: `ForProcess_OffWindows_IsTheUserProfileFolder`);
  232 `END` lines, none with a zero arrange, act or assert count. Whole project, fast: 3770 passed, 16 skipped.
- No test printed a `SLOW:` line.
- `AccountHomeDirectoryTests` print whether the profile folder was found and whether it matched, never the path
  itself, so nothing printed depends on the machine or operating system.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every test in the nine Curl.Cli.UnitTests files from AccountHomeDirectoryTests to CommandLineContinueAtNoClobberTests writes ARRANGE, ACT and ASSERT diagnostics
