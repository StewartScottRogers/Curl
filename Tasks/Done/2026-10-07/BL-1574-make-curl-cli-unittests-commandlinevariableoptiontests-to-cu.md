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
completed: 2026-10-07
---
# BL-1574 — Make Curl.Cli.UnitTests' CommandLineVariableOptionTests to CurlVersionTextTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Cli.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CommandLineVariableOptionTests.cs`, `CommandLineVersionOptionTests.cs`, `CommandLineWarningTests.cs`, `CommandLineWriteOutOptionTests.cs`, `CommandLineXattrOptionTests.cs`, `ConsolePasswordPromptTests.cs`, `CurlAiHelpTextTests.cs`, `CurlHelpTextTests.cs`, `CurlManualTests.cs`, `CurlOptionManualSectionTests.cs`, `CurlVersionTextTests.cs` (93 test methods, counted 2026-10-07).

## Context

- Split from BL-1459 (one per range of files, as BL-1463 and BL-1464 were split); BL-1459 keeps the whole-project checks and depends on this task. Read BL-1459's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR (ADR-0417); add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- What matters here: the full argument array (each argument quoted, so spaces and empty arguments show), the parsed option values the test checks, and for a refusal the exit code and the exact stderr line or lines.
- A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them. Every class in these files is in namespace `Curl.Cli` and named after its file, which the filter below uses.

## Acceptance criteria

- [x] `dotnet test Curl.Cli.UnitTests --filter "FullyQualifiedName~Curl.Cli.CommandLineVariableOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineVersionOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineWarningTests.|FullyQualifiedName~Curl.Cli.CommandLineWriteOutOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineXattrOptionTests.|FullyQualifiedName~Curl.Cli.ConsolePasswordPromptTests.|FullyQualifiedName~Curl.Cli.CurlAiHelpTextTests.|FullyQualifiedName~Curl.Cli.CurlHelpTextTests.|FullyQualifiedName~Curl.Cli.CurlManualTests.|FullyQualifiedName~Curl.Cli.CurlOptionManualSectionTests.|FullyQualifiedName~Curl.Cli.CurlVersionTextTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Cli.UnitTests -warnaserror` is clean and `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Cli.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Each file got the `TestContext`/`Diagnostics` pair. Parser files route every parse through an instance `Parse`
  helper that writes `ARRANGE arguments` (each quoted), `ActParse` (accepted, exit code, every stderr and warning
  line) and the option values its tests check; refusals write `AssertRefusal`. Help, manual and version files write
  the subject/columns as `ARRANGE`, the lines (quoted) as `ACT`, and an `ASSERT` or `DIFF` for every value checked.
  File and standard-input contents, keys and large text go out as `BYTES`. Calls into the code under test take the
  same arguments as before; no assertion was changed or removed.
- Counts (`Assert.` / `[TestMethod` / `[DataRow(`), before -> after, unchanged in every file: Variable 45/22/33,
  Version 23/9/14, Warning 7/6/0, WriteOut 35/13/14, Xattr 9/4/0, ConsolePasswordPrompt 7/4/5, AiHelp 43/15/27,
  Help 13/8/46, Manual 6/2/0, OptionManualSection 11/6/20, VersionText 4/4/2.
- The filtered run printed 226 `END` lines for 226 tests, none with a zero arrange, act or assert count.
  No test printed a `SLOW:` line. `Curl.Cli.UnitTests` fast run: 3770 passed, 16 skipped, 0 failed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every test in CommandLineVariableOptionTests to CurlVersionTextTests writes ARRANGE, ACT and ASSERT diagnostics
