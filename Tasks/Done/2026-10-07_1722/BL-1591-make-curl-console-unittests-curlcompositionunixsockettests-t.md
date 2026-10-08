---
id: BL-1591
title: Make Curl.Console.UnitTests' CurlCompositionUnixSocketTests to FlushEachWriteStreamTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1591 — Make Curl.Console.UnitTests' CurlCompositionUnixSocketTests to FlushEachWriteStreamTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Console.UnitTests` files (15 files, 90 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CurlCompositionUnixSocketTests.cs`, `CurlCompositionWsTests.cs`, `CurlCompositionWsTraceTests.cs`, `CurlEasyErrorTextTests.cs`, `CurlErrorBufferTests.cs`, `CurlTransportsTests.cs`, `DeferredOutputFileStreamTests.cs`, `DirectoryCreationFailureTests.cs`, `DiskWriteOutFileOpenerTests.cs`, `DumpHeaderOutputStreamTests.cs`, `EndPointRecordingConnectorTests.cs`, `EndPointRecordingDatagramConnectorTests.cs`, `EndPointReportingProtocolHandlerTests.cs`, `EtagSaveStreamTests.cs`, `FlushEachWriteStreamTests.cs`.

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

- 2026-10-07: All 15 files write ARRANGE, ACT and ASSERT or DIFF lines through `TestDiagnostics`; the filtered run printed 120 `END` lines (90 methods, data rows expanded), all 120 passed, none with a zero count.
- Counts before -> after (all 15 files): `Assert.` 206 -> 206, `[TestMethod` 90 -> 90, `[DataRow(` 42 -> 42. Per file `Assert.`, each unchanged: UnixSocket 15, Ws 26, WsTrace 8, EasyErrorText 4, ErrorBuffer 5, Transports 31, DeferredOutputFileStream 29, DirectoryCreationFailure 1, DiskWriteOutFileOpener 11, DumpHeaderOutputStream 21, EndPointRecordingConnector 15, EndPointRecordingDatagramConnector 5, EndPointReportingProtocolHandler 5, EtagSaveStream 17, FlushEachWriteStream 13.
- No test printed a `SLOW:` line; all run in milliseconds, so no `PHASE` lines were added.
- Choices: temp-directory paths print as `<temp>/...` and standard error prints with carriage returns removed, so nothing printed depends on the operating system; the credential-encoding test prints whether the encoding is the platform's, not the encoding. Expression-bodied tests became block bodies so they can write lines; `CurlCompositionWsTests.RunAsync`, `CurlTransportsTests.Options` and `DiskWriteOutFileOpenerTests.AssertRefused` became instance methods so each writes the command line or path and results for its test.
- `dotnet build Curl.Console.UnitTests -warnaserror` clean; fast tests 2661 passed, 24 skipped, 0 failed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. All 15 files write descriptive diagnostics; 120 END lines, none zero; counts unchanged; build and fast tests green.
