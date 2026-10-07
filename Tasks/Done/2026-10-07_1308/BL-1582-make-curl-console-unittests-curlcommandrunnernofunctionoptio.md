---
id: BL-1582
title: Make Curl.Console.UnitTests' CurlCommandRunnerNoFunctionOptionTests to CurlCommandRunnerProxyContextTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1582 — Make Curl.Console.UnitTests' CurlCommandRunnerNoFunctionOptionTests to CurlCommandRunnerProxyContextTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Console.UnitTests` files (10 files, 92 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CurlCommandRunnerNoFunctionOptionTests.cs`, `CurlCommandRunnerOptionGroupConnectionReuseTests.cs`, `CurlCommandRunnerOutNullTests.cs`, `CurlCommandRunnerOutputFileNameTests.cs`, `CurlCommandRunnerParallelProgressMeterTests.cs`, `CurlCommandRunnerParallelTests.cs`, `CurlCommandRunnerPop3TransferEventTests.cs`, `CurlCommandRunnerProgressBarTests.cs`, `CurlCommandRunnerProgressMeterTests.cs`, `CurlCommandRunnerProxyContextTests.cs`.

## Context

- Split from BL-1461 (one task per range of files, as its Notes direct); BL-1461 keeps the whole-project checks and depends on this task. Follow BL-1461's Context: what matters in this project (command line, the scripted connector's script, request bytes, stdout and stderr, exit code), `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system. BL-1541 and BL-1542 show the pattern in `Curl.Networking.UnitTests`.
- "These classes' filter" below is one `FullyQualifiedName~Curl.Console.<Class>.` term per class the listed files declare (a partial class's files count once), joined with `|`.

## Acceptance criteria

- [x] `dotnet test Curl.Console.UnitTests --filter "<these classes' filter>" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Console.UnitTests -warnaserror` is clean and `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Console.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Filter run 2026-10-07: 112 tests, 111 ran and each printed an `END` line, none with arrange, act or assert 0; 1 skipped on Windows by its OS condition (`RunAsync_VerboseRetrWithStlsOffWindows_WritesTheOpenSslTrustLineAroundTheUpgrade`).
- Counts across the ten files, before -> after: `Assert.` 218 -> 219 (one `Assert.IsNotNull` added where the cancelled test now captures the thrown exception), `[TestMethod` 92 -> 92, `[DataRow(` 29 -> 29.
- No test printed a `SLOW:` line; no follow-up task.
- No `ASSERT` line printed a differing expected and actual value, and every `DIFF` line said `equal`.
- Choices: printed text is LF-normalised (progress-bar output also shows each CR as `\r`) and no temporary path is printed, so the log reads the same on every OS; a few tests that never asserted their exit code print an `ASSERT exit code` diagnostic only (no new `Assert.`), and the run confirmed it matches.
- `dotnet format --verify-no-changes` reports only a missing final newline in `CurlCompositionPlatformLibraryTests.cs`, a file outside this task's range, left untouched.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every test in the ten files writes diagnostics; build clean, fast tests green
