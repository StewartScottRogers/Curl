---
id: BL-1570
title: Make Curl.Cli.UnitTests' CommandLineRemoteNameOptionTests to CommandLineSkipExistingTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Cli.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1570 — Make Curl.Cli.UnitTests' CommandLineRemoteNameOptionTests to CommandLineSkipExistingTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Cli.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CommandLineRemoteNameOptionTests.cs`, `CommandLineRemoteTimeOptionTests.cs`, `CommandLineRemoveOnErrorTests.cs`, `CommandLineResolveOptionTests.cs`, `CommandLineRetryAndSpeedOptionTests.cs`, `CommandLineSchannelBuildRefusalTests.cs`, `CommandLineSizeAndOffsetTests.cs`, `CommandLineSkipExistingTests.cs` (94 test methods, counted 2026-10-07).

## Context

- Split from BL-1459 (one per range of files, as BL-1463 and BL-1464 were split); BL-1459 keeps the whole-project checks and depends on this task. Read BL-1459's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR (ADR-0417); add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- What matters here: the full argument array (each argument quoted, so spaces and empty arguments show), the parsed option values the test checks, and for a refusal the exit code and the exact stderr line or lines.
- A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them. Every class in these files is in namespace `Curl.Cli` and named after its file, which the filter below uses.

## Acceptance criteria

- [x] `dotnet test Curl.Cli.UnitTests --filter "FullyQualifiedName~Curl.Cli.CommandLineRemoteNameOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineRemoteTimeOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineRemoveOnErrorTests.|FullyQualifiedName~Curl.Cli.CommandLineResolveOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineRetryAndSpeedOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineSchannelBuildRefusalTests.|FullyQualifiedName~Curl.Cli.CommandLineSizeAndOffsetTests.|FullyQualifiedName~Curl.Cli.CommandLineSkipExistingTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Cli.UnitTests -warnaserror` is clean and `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Cli.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Approach: each class gets `TestContext`, a `Diagnostics` property and an instance `Parse` helper that writes `ARRANGE arguments` (each argument quoted), the parse result through `CommandLineParseDiagnostics.ActParse`, and `ACT` lines for the options its file pins (url outputs, resolve and connect-to entries, retry and speed values, and so on). Every acceptance assert is preceded by `ASSERT accepted`; every refusal check writes `ASSERT accepted`, `exit code` and the quoted `stderr` lines through a new shared `CommandLineParseDiagnostics.AssertRefusal`. `CommandLineSizeAndOffsetTests` wraps `CommandLineNumber.ParseSize`/`ParseOffset` the same way; the null-argument tests write the calls, the thrown parameter names and their asserts. No test logic or `Assert.` call was changed or removed.
- Counts before -> after (`Assert.` / `[TestMethod` / `[DataRow(`), unchanged in every file: RemoteName 80/26/18, RemoteTime 8/4/2, RemoveOnError 17/8/15, Resolve 13/7/19, RetryAndSpeed 39/20/125, SchannelBuildRefusal 19/17/32, SizeAndOffset 22/9/78, SkipExisting 6/3/4.
- Filtered detailed run: 341 tests, 340 run (1 skipped on Windows by `OSCondition`), 340 `END` lines, none with a zero arrange, act or assert count.
- No test printed a `SLOW:` line; no follow-up task needed.
- Project fast tests: 3770 passed, 16 skipped, 0 failed; `dotnet build Curl.Cli.UnitTests -warnaserror` clean.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. CommandLineRemoteNameOptionTests to CommandLineSkipExistingTests write ARRANGE, ACT and ASSERT diagnostics for all 340 tests that run
