---
id: BL-1562
title: Make Curl.Cli.UnitTests' CommandLineFormOptionTests to CommandLineGlobOffOptionTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Cli.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1562 — Make Curl.Cli.UnitTests' CommandLineFormOptionTests to CommandLineGlobOffOptionTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Cli.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CommandLineFormOptionTests.cs`, `CommandLineFtpAccountPretAndCccOptionTests.cs`, `CommandLineFtpActiveModeAndSslOptionTests.cs`, `CommandLineFtpOptionTests.cs`, `CommandLineGlobOffOptionTests.cs` (94 test methods, counted 2026-10-07).

## Context

- Split from BL-1459 (one per range of files, as BL-1463 and BL-1464 were split); BL-1459 keeps the whole-project checks and depends on this task. Read BL-1459's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR (ADR-0417); add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- What matters here: the full argument array (each argument quoted, so spaces and empty arguments show), the parsed option values the test checks, and for a refusal the exit code and the exact stderr line or lines.
- A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them. Every class in these files is in namespace `Curl.Cli` and named after its file, which the filter below uses.

## Acceptance criteria

- [x] `dotnet test Curl.Cli.UnitTests --filter "FullyQualifiedName~Curl.Cli.CommandLineFormOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineFtpAccountPretAndCccOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineFtpActiveModeAndSslOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineFtpOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineGlobOffOptionTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Cli.UnitTests -warnaserror` is clean and `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Cli.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Same shape as BL-1561: each class gets `TestContext` and a `Parse` wrapper that writes `ARRANGE arguments` (quoted) and `ACT` lines through `CommandLineParseDiagnostics`; every test writes `ASSERT` lines for the values it checks, ahead of the unchanged MSTest assertions. In `CommandLineFormOptionTests` the shared `AssertPart`, `AssertAccepted`, `AssertRefused` and `AssertRefusedAtTransferSetup` write them (content, type and file name quoted so empty and blank values show), and the headers-file test writes the file as `BYTES`. Option values are read with `?.` in the diagnostic lines so a refusal still reaches the real assertion.
- Counts (`Assert.`, `[TestMethod`, `[DataRow(`), before = after: Form 27/45/40, FtpAccountPretAndCcc 33/12/27, FtpActiveModeAndSsl 39/19/43, Ftp 33/13/27, GlobOff 12/5/2.
- The filtered detailed run printed 196 `END` lines (196 passed), none with a zero count. No test printed a `SLOW:` line.
- `dotnet build Curl.Cli.UnitTests -warnaserror` clean; `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"`: 3770 passed, 16 skipped, 0 failed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. All 94 tests in CommandLineFormOptionTests to CommandLineGlobOffOptionTests write ARRANGE, ACT and ASSERT diagnostics
