---
id: BL-1575
title: Make Curl.Cli.UnitTests' DefaultConfigFileSearchTests to LibcurlSourceCodeTransferFileTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Cli.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1575 — Make Curl.Cli.UnitTests' DefaultConfigFileSearchTests to LibcurlSourceCodeTransferFileTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Cli.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `DefaultConfigFileSearchTests.cs`, `DiskDataFileReaderTests.cs`, `LibcurlSourceCodeOptionCoverageTests.cs`, `LibcurlSourceCodeOptionTests.cs`, `LibcurlSourceCodeProtocolOptionTests.cs`, `LibcurlSourceCodeProxyTlsAndAuthenticationTests.cs`, `LibcurlSourceCodeTests.cs`, `LibcurlSourceCodeTransferFileTests.cs` (96 test methods, counted 2026-10-07).

## Context

- Split from BL-1459 (one per range of files, as BL-1463 and BL-1464 were split); BL-1459 keeps the whole-project checks and depends on this task. Read BL-1459's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR (ADR-0417); add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- What matters here: the full argument array (each argument quoted, so spaces and empty arguments show), the parsed option values the test checks, and for a refusal the exit code and the exact stderr line or lines.
- A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them. Every class in these files is in namespace `Curl.Cli` and named after its file, which the filter below uses.

## Acceptance criteria

- [x] `dotnet test Curl.Cli.UnitTests --filter "FullyQualifiedName~Curl.Cli.DefaultConfigFileSearchTests.|FullyQualifiedName~Curl.Cli.DiskDataFileReaderTests.|FullyQualifiedName~Curl.Cli.LibcurlSourceCodeOptionCoverageTests.|FullyQualifiedName~Curl.Cli.LibcurlSourceCodeOptionTests.|FullyQualifiedName~Curl.Cli.LibcurlSourceCodeProtocolOptionTests.|FullyQualifiedName~Curl.Cli.LibcurlSourceCodeProxyTlsAndAuthenticationTests.|FullyQualifiedName~Curl.Cli.LibcurlSourceCodeTests.|FullyQualifiedName~Curl.Cli.LibcurlSourceCodeTransferFileTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Cli.UnitTests -warnaserror` is clean and `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Cli.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts before -> after (`Assert.` / `[TestMethod` / `[DataRow(`): DefaultConfigFileSearchTests 9/9/0 -> 9/9/0; DiskDataFileReaderTests 59/26/42 -> 59/26/42; LibcurlSourceCodeOptionCoverageTests 3/2/0 -> 3/2/0; LibcurlSourceCodeOptionTests 8/8/59 -> 8/8/59; LibcurlSourceCodeProtocolOptionTests 18/16/61 -> 18/16/61; LibcurlSourceCodeProxyTlsAndAuthenticationTests 11/8/120 -> 11/8/120; LibcurlSourceCodeTests 18/12/15 -> 18/12/15; LibcurlSourceCodeTransferFileTests 18/15/14 -> 18/15/14.
- The filter runs 376 tests on Windows: 372 pass, each with an `END` line and none with a zero arrange, act or assert count; 4 are off-Windows `OSCondition` tests, skipped here, which write no `END` line because they do not run.
- Shared wrappers write the lines for the tests that use them: each libcurl file's `Parse`/`GenerateFor` writes `ARRANGE arguments` (every argument quoted, through `CommandLineParseDiagnostics.ArrangeArguments`) and `ACT source`; `DefaultConfigFileSearchTests.Search` writes the platform, executable directory and environment, and `WrittenCandidatePaths` the paths and their `DIFF`; `DiskDataFileReaderTests` prints scratch paths relative to `<temp>` with `/` separators so nothing depends on the OS or the machine.
- No test printed a `SLOW:` line (the whole set runs in under 5 seconds), so no follow-up task.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every test in the eight files writes ARRANGE, ACT and ASSERT/DIFF diagnostics; build clean, fast tests green
