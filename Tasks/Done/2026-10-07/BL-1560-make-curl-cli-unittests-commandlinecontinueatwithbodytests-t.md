---
id: BL-1560
title: Make Curl.Cli.UnitTests' CommandLineContinueAtWithBodyTests to CommandLineDnsOptionTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Cli.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1560 — Make Curl.Cli.UnitTests' CommandLineContinueAtWithBodyTests to CommandLineDnsOptionTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Cli.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CommandLineContinueAtWithBodyTests.cs`, `CommandLineCookieOptionTests.cs`, `CommandLineCreateFileModeTests.cs`, `CommandLineDataFileTests.cs`, `CommandLineDefaultConfigFileTests.cs`, `CommandLineDiagnosticLogOptionTests.cs`, `CommandLineDisallowUsernameInUrlTests.cs`, `CommandLineDnsOptionTests.cs` (91 test methods, counted 2026-10-07).

## Context

- Split from BL-1459 (one per range of files, as BL-1463 and BL-1464 were split); BL-1459 keeps the whole-project checks and depends on this task. Read BL-1459's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR (ADR-0417); add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- What matters here: the full argument array (each argument quoted, so spaces and empty arguments show), the parsed option values the test checks, and for a refusal the exit code and the exact stderr line or lines.
- A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them. Every class in these files is in namespace `Curl.Cli` and named after its file, which the filter below uses.

## Acceptance criteria

- [x] `dotnet test Curl.Cli.UnitTests --filter "FullyQualifiedName~Curl.Cli.CommandLineContinueAtWithBodyTests.|FullyQualifiedName~Curl.Cli.CommandLineCookieOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineCreateFileModeTests.|FullyQualifiedName~Curl.Cli.CommandLineDataFileTests.|FullyQualifiedName~Curl.Cli.CommandLineDefaultConfigFileTests.|FullyQualifiedName~Curl.Cli.CommandLineDiagnosticLogOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineDisallowUsernameInUrlTests.|FullyQualifiedName~Curl.Cli.CommandLineDnsOptionTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Cli.UnitTests -warnaserror` is clean and `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Cli.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Approach (as BL-1559): each file gets a `TestContext` and a private `Parse` wrapper that writes the
  quoted argument array (`ArrangeArguments`) and the parse result (`ActParse`: accepted, a refusal's exit
  code and stderr lines, warning lines), both from BL-1559's `CommandLineParseDiagnostics`. Shared assert
  helpers (`AssertRefused`, `AssertRefusedAtTransferSetup`, Dns' `Accept`, `AssertLog`, `AssertCookie`,
  `AssertPostData`) became instance methods that write `ASSERT` lines; every other test writes an `ASSERT`
  line for the values it checks just before its own assertions. Config, data and standard input bytes go
  through `BYTES`, post data through `BYTES` and `DIFF`. The three `ThrowsExactly` tests write the null
  argument, the exception and its parameter. No assertion was changed.
- Counts in the eight files, before -> after: `Assert.` 186 -> 186, `[TestMethod` 91 -> 91, `[DataRow(`
  109 -> 109 (diagnostic calls are `diagnostics.Assert(`, which does not match `Assert.`).
- The filtered detailed run: 165 tests (91 methods with their data rows), 165 `END` lines, none with a zero
  arrange, act or assert count. Fast run of the project: 3770 passed, 16 skipped, 0 failed.
- No test printed a `SLOW:` line.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every test from CommandLineContinueAtWithBodyTests to CommandLineDnsOptionTests writes arrange, act and assert diagnostics
