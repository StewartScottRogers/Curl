---
id: BL-1592
title: Make Curl.Console.UnitTests' HandBuiltKerberosSourcesTests to OutputFileOpenWarningTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1592 — Make Curl.Console.UnitTests' HandBuiltKerberosSourcesTests to OutputFileOpenWarningTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Console.UnitTests` files (20 files, 91 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `HandBuiltKerberosSourcesTests.cs`, `HappyEyeballsTimeoutMappingTests.cs`, `HeaderLineTeeStreamTests.cs`, `HoldableStreamTests.cs`, `HttpContinueWaitMappingTests.cs`, `HttpUnixSocketMappingTests.cs`, `HttpVersionMappingTests.cs`, `KerberosDiskFileReaderTests.cs`, `KerberosDiskFileWriterTests.cs`, `LateBoundDiagnosticLogTests.cs`, `LateBoundSecurityContextFactoryTests.cs`, `LineFeedToCrLfStreamTests.cs`, `MultipartFormPartMappingTests.cs`, `MultiStateTraceEventsTests.cs`, `NativeExtendedAttributeWriterTests.cs`, `NativeRegularFileTestTests.cs`, `NoCryptoEnginesTests.cs`, `OutputFileDirectoriesTests.cs`, `OutputFileExtendedAttributesTests.cs`, `OutputFileOpenWarningTests.cs`.

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

- Every test in the 20 files now writes ARRANGE, ACT and ASSERT/DIFF lines through `TestDiagnostics`; no assertion or test logic changed. Where an `Assert.ThrowsExactly` result is now kept for its ACT line, each exception is asserted once, as before.
- Counts in these files, before -> after: `Assert.` 160 -> 160, `[TestMethod` 91 -> 91, `[DataRow(` 64 -> 64.
- The classes' filter on Windows runs 141 tests: 133 pass, each with an `END` line and none with a zero count; 8 are skipped by their `OSCondition` (off-Windows tests), so they print no `END`.
- No test printed a `SLOW:` line.
- `dotnet build Curl.Console.UnitTests -warnaserror` clean; fast tests 2661 passed, 24 skipped, 0 failed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every test in HandBuiltKerberosSourcesTests to OutputFileOpenWarningTests writes Arrange, Act and Assert diagnostics
