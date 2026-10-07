---
id: BL-1614
title: Make Curl.Protocol.Http.UnitTests' HttpProtocolHandlerTests.DigestStale to HttpProtocolHandlerTests.Http2Trace tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1614 — Make Curl.Protocol.Http.UnitTests' HttpProtocolHandlerTests.DigestStale to HttpProtocolHandlerTests.Http2Trace tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Protocol.Http.UnitTests` files (12 files, 97 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `HttpProtocolHandlerTests.DigestStale.cs`, `HttpProtocolHandlerTests.ErrorWhileSending.cs`, `HttpProtocolHandlerTests.Events.cs`, `HttpProtocolHandlerTests.ExpectationFailed.cs`, `HttpProtocolHandlerTests.ExpectationFailedLoop.cs`, `HttpProtocolHandlerTests.ExpectationFailedWhileSending.cs`, `HttpProtocolHandlerTests.FrameLog.cs`, `HttpProtocolHandlerTests.H2cUpgrade.cs`, `HttpProtocolHandlerTests.HeadInfoLines.cs`, `HttpProtocolHandlerTests.Http09.cs`, `HttpProtocolHandlerTests.Http2.cs`, `HttpProtocolHandlerTests.Http2Trace.cs`.

## Context

- Split from BL-1476 (one task per range of files, as its Notes direct); BL-1476 keeps the whole-project checks and depends on this task. Follow BL-1476's Context: what matters in this project (request line and headers, the scripted response, the parsed status, headers and body, each redirect or authentication step, the `CurlExitCode` with its error text), `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system. BL-1478 (`Curl.Protocol.Ldap.UnitTests`) shows the pattern.
- `HttpProtocolHandlerTests` is one partial class (563 test methods) spread over seven tasks, BL-1612 to BL-1618. "Its filter" below is `FullyQualifiedName~Curl.Protocol.Http.HttpProtocolHandlerTests.`, and the first criterion's check applies only to the `END` lines of test methods declared in this task's files. A shared helper of that class (in `HttpProtocolHandlerTests.cs`) may write lines for the tests that use it.

## Acceptance criteria

- [x] `dotnet test Curl.Protocol.Http.UnitTests --filter "<its filter>" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none for a test method declared in these files matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Protocol.Http.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Http.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Protocol.Http.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts in the twelve files, before -> after: `Assert.` 365 -> 365, `[TestMethod` 97 -> 97, `[DataRow(` 32 -> 32. Only lines were added; where a test discarded the handler's result it now keeps it as `TransferResult result` so `WriteResult` can print it.
- The class filter ran 798 tests (785 passed, 13 skipped, which print no END line); all 97 test methods in these files printed END lines with nonzero arrange, act and assert counts.
- No test printed a `SLOW:` line.
- Added one private helper, `WriteLogLines(RecordingDiagnosticLog)`, in `HttpProtocolHandlerTests.FrameLog.cs`. `Http2TraceLinesAsync` is static, so its tests write their act line from the returned lines.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. All 97 test methods in the twelve files write arrange, act and assert diagnostics; build clean, fast tests green.
