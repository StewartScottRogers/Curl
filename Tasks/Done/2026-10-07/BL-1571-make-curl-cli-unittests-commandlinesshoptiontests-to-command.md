---
id: BL-1571
title: Make Curl.Cli.UnitTests' CommandLineSshOptionTests to CommandLineTlsHandshakeOptionTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Cli.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1571 — Make Curl.Cli.UnitTests' CommandLineSshOptionTests to CommandLineTlsHandshakeOptionTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Cli.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CommandLineSshOptionTests.cs`, `CommandLineTimeConditionOptionTests.cs`, `CommandLineTimeoutOptionTests.cs`, `CommandLineTlsHandshakeOptionTests.cs` (80 test methods, counted 2026-10-07).

## Context

- Split from BL-1459 (one per range of files, as BL-1463 and BL-1464 were split); BL-1459 keeps the whole-project checks and depends on this task. Read BL-1459's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR (ADR-0417); add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- What matters here: the full argument array (each argument quoted, so spaces and empty arguments show), the parsed option values the test checks, and for a refusal the exit code and the exact stderr line or lines.
- A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them. Every class in these files is in namespace `Curl.Cli` and named after its file, which the filter below uses.

## Acceptance criteria

- [x] `dotnet test Curl.Cli.UnitTests --filter "FullyQualifiedName~Curl.Cli.CommandLineSshOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineTimeConditionOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineTimeoutOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineTlsHandshakeOptionTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Cli.UnitTests -warnaserror` is clean and `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Cli.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts before -> after (Assert. / [TestMethod / [DataRow(): CommandLineSshOptionTests 31/15/16 -> 31/15/16; CommandLineTimeConditionOptionTests 55/21/20 -> 55/21/20; CommandLineTimeoutOptionTests 52/21/45 -> 54/21/45 (ParseSeconds_NullArgument_Throws now captures each ParamName for its diagnostics too, and keeps both original assertions); CommandLineTlsHandshakeOptionTests 70/23/51 -> 70/23/51.
- The filtered detailed run: 178 tests, 175 run (3 skipped by OSCondition on Windows), 175 END lines, none with a zero arrange, act or assert count.
- No test printed a SLOW: line.
- Each class got a TestContext and an instance Parse wrapper that writes the quoted argument array (ARRANGE), the parse outcome through CommandLineParseDiagnostics.ActParse plus the option values that class pins (ACT); each test writes ASSERT lines for what it checks. A time condition prints as kind, Unix seconds and an ISO 8601 instant through FormattableString.Invariant so nothing depends on the machine's culture or OS. CommandLineTimeoutOptionTests' direct CommandLineNumber tests write their own ARRANGE/ACT lines through a ParseSeconds wrapper.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Diagnostics written by all 80 test methods; build clean, fast tests green
