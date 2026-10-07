---
id: BL-1565
title: Make Curl.Cli.UnitTests' CommandLineNegationTests to CommandLineNoFunctionOptionTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Cli.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1565 — Make Curl.Cli.UnitTests' CommandLineNegationTests to CommandLineNoFunctionOptionTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Cli.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CommandLineNegationTests.cs`, `CommandLineNetrcOptionTests.cs`, `CommandLineNextGroupTests.cs`, `CommandLineNoBufferTests.cs`, `CommandLineNoFunctionOptionTests.cs` (82 test methods, counted 2026-10-07).

## Context

- Split from BL-1459 (one per range of files, as BL-1463 and BL-1464 were split); BL-1459 keeps the whole-project checks and depends on this task. Read BL-1459's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR (ADR-0417); add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- What matters here: the full argument array (each argument quoted, so spaces and empty arguments show), the parsed option values the test checks, and for a refusal the exit code and the exact stderr line or lines.
- A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them. Every class in these files is in namespace `Curl.Cli` and named after its file, which the filter below uses.

## Acceptance criteria

- [x] `dotnet test Curl.Cli.UnitTests --filter "FullyQualifiedName~Curl.Cli.CommandLineNegationTests.|FullyQualifiedName~Curl.Cli.CommandLineNetrcOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineNextGroupTests.|FullyQualifiedName~Curl.Cli.CommandLineNoBufferTests.|FullyQualifiedName~Curl.Cli.CommandLineNoFunctionOptionTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Cli.UnitTests -warnaserror` is clean and `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Cli.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Every test in the five files now writes ARRANGE (the argument array, each argument quoted; config-file bytes as BYTES), ACT (accepted, exit code and stderr lines of a refusal, warnings, and in NextGroupTests every group's URLs and any refusal after the groups) and ASSERT lines, through a `Parse` helper per class and `CommandLineParseDiagnostics`. No test logic or assertion changed.
- Added `CommandLineParseDiagnostics.Peek`: a diagnostic line reading `result.Refusal?.X` directly made the compiler treat `Refusal` as maybe-null in the real assertions after it (CS8602); passing it through `Peek` keeps the null state, so no assertion needed a `!`.
- Counts (`Assert.` / `[TestMethod` / `[DataRow(`), before = after: Negation 24/28/4, NetrcOption 19/8/26, NextGroup 88/23/14, NoBuffer 13/6/7, NoFunctionOption 45/17/33.
- The acceptance filter ran 149 tests, 149 END lines, none with a zero arrange, act or assert count. Curl.Cli.UnitTests fast run: 3770 passed, 16 skipped.
- No test printed a `SLOW:` line.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. CommandLineNegationTests to CommandLineNoFunctionOptionTests (149 tests) write ARRANGE, ACT and ASSERT diagnostics
