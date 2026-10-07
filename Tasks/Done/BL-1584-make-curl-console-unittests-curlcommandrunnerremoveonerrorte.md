---
id: BL-1584
title: Make Curl.Console.UnitTests' CurlCommandRunnerRemoveOnErrorTests to CurlCommandRunnerSmtpTransferEventTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1584 — Make Curl.Console.UnitTests' CurlCommandRunnerRemoveOnErrorTests to CurlCommandRunnerSmtpTransferEventTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Console.UnitTests` files (12 files, 90 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CurlCommandRunnerRemoveOnErrorTests.cs`, `CurlCommandRunnerRequestMethodConflictTests.cs`, `CurlCommandRunnerRequestTargetTests.cs`, `CurlCommandRunnerResolveEntryTests.cs`, `CurlCommandRunnerRetryTests.cs`, `CurlCommandRunnerRunningProgressMeterTests.cs`, `CurlCommandRunnerSchannelBuildRefusalTests.cs`, `CurlCommandRunnerSharedDnsCacheTests.cs`, `CurlCommandRunnerSizeDeliveredTests.cs`, `CurlCommandRunnerSkipExistingTests.cs`, `CurlCommandRunnerSmbTransferEventTests.cs`, `CurlCommandRunnerSmtpTransferEventTests.cs`.

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

- Counts in the 12 files, before -> after: `Assert.` 235 -> 235, `[TestMethod` 90 -> 90, `[DataRow(` 38 -> 38 (the only edits to existing assertions lift a multi-line expected value into a local so the `Diff` line and the `Assert.AreEqual` share it, in the SMTP and SkipExisting tests).
- The filtered detailed run: 113 tests ran (1 more is skipped on Windows by its `OSCondition`), 113 `END` lines, none with a zero arrange, act or assert count.
- No test printed a `SLOW:` line; the slowest took about 250 ms, so no follow-up task.
- Shared run helpers write the ARRANGE (arguments, handler or server script) and ACT (exit code, stdout, stderr, request or file results) lines inside a `run` PHASE; text is LF-normalised so the log reads the same on every OS.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. All 12 files write diagnostics; 113 END lines, none zero; build clean, fast tests green.
