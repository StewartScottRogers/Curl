---
id: BL-1567
title: Make Curl.Cli.UnitTests' CommandLineParserTests to CommandLineProgressOptionTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Cli.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1567 — Make Curl.Cli.UnitTests' CommandLineParserTests to CommandLineProgressOptionTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Cli.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CommandLineParserTests.cs`, `CommandLinePasswordPromptTests.cs`, `CommandLinePostDataOptionTests.cs`, `CommandLineProgressOptionTests.cs` (100 test methods, counted 2026-10-07).

## Context

- Split from BL-1459 (one per range of files, as BL-1463 and BL-1464 were split); BL-1459 keeps the whole-project checks and depends on this task. Read BL-1459's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR (ADR-0417); add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- What matters here: the full argument array (each argument quoted, so spaces and empty arguments show), the parsed option values the test checks, and for a refusal the exit code and the exact stderr line or lines.
- A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them. Every class in these files is in namespace `Curl.Cli` and named after its file, which the filter below uses.

## Acceptance criteria

- [x] `dotnet test Curl.Cli.UnitTests --filter "FullyQualifiedName~Curl.Cli.CommandLineParserTests.|FullyQualifiedName~Curl.Cli.CommandLinePasswordPromptTests.|FullyQualifiedName~Curl.Cli.CommandLinePostDataOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineProgressOptionTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Cli.UnitTests -warnaserror` is clean and `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Cli.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Each class got a `TestContext` and an instance `Parse` helper that writes `ARRANGE arguments` (each argument quoted, through `CommandLineParseDiagnostics`) and the parse result as `ACT` lines (accepted, exit code, every stderr and warning line). Per file it adds what its tests check: `CommandLineParserTests` the URLs, output files and after-transfer warnings; `CommandLinePasswordPromptTests` the prompt answer and the prompts asked; `CommandLinePostDataOptionTests` every in-memory file and standard input as `BYTES`, the reads, the body as `BYTES` and a `DIFF body`. Each test then writes an `ASSERT` (or `DIFF`) for what it checks; the two `ArgumentNullException` tests write their arguments, the exception and its parameter.
- Counts (`Assert.`, `[TestMethod`, `[DataRow(`), before -> after: CommandLineParserTests 102/57/28 -> 102/57/28; CommandLinePasswordPromptTests 52/16/10 -> 52/16/10; CommandLinePostDataOptionTests 36/20/22 -> 36/20/22; CommandLineProgressOptionTests 17/7/18 -> 17/7/18. No assertion was changed or removed.
- The acceptance filter runs 155 tests: 155 `END` lines, none with a zero arrange, act or assert count.
- No test printed a `SLOW:` line.
- Only test code changed, so no library's coverage moved; Measure-CodeQuality.ps1 was not run.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every test in CommandLineParserTests to CommandLineProgressOptionTests writes ARRANGE, ACT and ASSERT diagnostics
