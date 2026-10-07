---
id: BL-1574
title: Make Curl.Cli.UnitTests' CommandLineVariableOptionTests to CurlVersionTextTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Cli.UnitTests]
requirement: none
created: 2026-10-07
completed:
---
# BL-1574 — Make Curl.Cli.UnitTests' CommandLineVariableOptionTests to CurlVersionTextTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Cli.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CommandLineVariableOptionTests.cs`, `CommandLineVersionOptionTests.cs`, `CommandLineWarningTests.cs`, `CommandLineWriteOutOptionTests.cs`, `CommandLineXattrOptionTests.cs`, `ConsolePasswordPromptTests.cs`, `CurlAiHelpTextTests.cs`, `CurlHelpTextTests.cs`, `CurlManualTests.cs`, `CurlOptionManualSectionTests.cs`, `CurlVersionTextTests.cs` (93 test methods, counted 2026-10-07).

## Context

- Split from BL-1459 (one per range of files, as BL-1463 and BL-1464 were split); BL-1459 keeps the whole-project checks and depends on this task. Read BL-1459's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR (ADR-0417); add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- What matters here: the full argument array (each argument quoted, so spaces and empty arguments show), the parsed option values the test checks, and for a refusal the exit code and the exact stderr line or lines.
- A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them. Every class in these files is in namespace `Curl.Cli` and named after its file, which the filter below uses.

## Acceptance criteria

- [ ] `dotnet test Curl.Cli.UnitTests --filter "FullyQualifiedName~Curl.Cli.CommandLineVariableOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineVersionOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineWarningTests.|FullyQualifiedName~Curl.Cli.CommandLineWriteOutOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineXattrOptionTests.|FullyQualifiedName~Curl.Cli.ConsolePasswordPromptTests.|FullyQualifiedName~Curl.Cli.CurlAiHelpTextTests.|FullyQualifiedName~Curl.Cli.CurlHelpTextTests.|FullyQualifiedName~Curl.Cli.CurlManualTests.|FullyQualifiedName~Curl.Cli.CurlOptionManualSectionTests.|FullyQualifiedName~Curl.Cli.CurlVersionTextTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [ ] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [ ] `dotnet build Curl.Cli.UnitTests -warnaserror` is clean and `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` passes.
- [ ] The task's commits change only files under `Curl.Cli.UnitTests/` and this task file.
- [ ] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

## Log

- 2026-10-07: Created.