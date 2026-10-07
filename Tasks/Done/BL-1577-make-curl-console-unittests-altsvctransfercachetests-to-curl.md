---
id: BL-1577
title: Make Curl.Console.UnitTests' AltSvcTransferCacheTests to CurlCommandRunnerAltSvcTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1577 — Make Curl.Console.UnitTests' AltSvcTransferCacheTests to CurlCommandRunnerAltSvcTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Console.UnitTests` files (13 files, 97 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `AltSvcTransferCacheTests.cs`, `AwsSigV4HttpAuthenticatorTests.cs`, `ClientReaderResetTraceEventsTests.cs`, `ClientWriterTraceEventsTests.cs`, `ClosedStandardOutputStreamTests.cs`, `ConnectionEndPointRecorderTests.cs`, `ConnectionIdRecordingTransferEventsTests.cs`, `ConnectReplyHeadWritingEventsTests.cs`, `ContentDispositionFileNameTests.cs`, `CookieEngineTests.cs`, `CRuntimeErrorNumbersTests.cs`, `CurlCommandRunnerAiHelpTests.cs`, `CurlCommandRunnerAltSvcTests.cs`.

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

- Counts in the 13 files, before -> after: `Assert.` 170 -> 170, `[TestMethod` 97 -> 97, `[DataRow(` 35 -> 35 (the new `Diagnostics.Assert(` calls are extra and do not match `Assert.`).
- Filtered detailed run: 121 tests (data rows expanded), 121 `END` lines, none with a zero arrange, act or assert count.
- `SLOW:` lines: none.
- Choices: the expression-bodied tests in `ContentDispositionFileNameTests` became block bodies with the header line in a local, printed with `\r` and `\n` escaped so the output does not depend on the OS. `CurlCommandRunnerAltSvcTests.RunAsync` writes the command line, cache file, stdout, stderr and request bytes and a `run` phase for every test. Where an assert's value is a pure call, the diagnostics call it again rather than changing the assert. Tests whose actions run inside `Assert.Throws` write a marker Act line instead of a result.
- Whole project: `dotnet build -warnaserror` clean; the fast tests passed 2661, with 24 skipped.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. The 13 Curl.Console.UnitTests files from AltSvcTransferCacheTests to CurlCommandRunnerAltSvcTests write ARRANGE, ACT and ASSERT diagnostics in every test
