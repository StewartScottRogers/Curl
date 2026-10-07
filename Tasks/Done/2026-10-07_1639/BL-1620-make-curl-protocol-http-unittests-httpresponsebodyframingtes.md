---
id: BL-1620
title: Make Curl.Protocol.Http.UnitTests' HttpResponseBodyFramingTests to ReadAheadConnectionStreamTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1620 — Make Curl.Protocol.Http.UnitTests' HttpResponseBodyFramingTests to ReadAheadConnectionStreamTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Protocol.Http.UnitTests` files (12 files, 106 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `HttpResponseBodyFramingTests.cs`, `HttpResponseBodyReaderTests.cs`, `HttpResponseHeadReaderTests.cs`, `HttpStatusLineTests.cs`, `HttpStreamOpenedLinesTests.cs`, `HttpTimeConditionLimitTests.cs`, `HttpTransferEncodingTests.cs`, `HttpTransferMessagesTests.cs`, `HttpTransferProgressTests.cs`, `HttpUrlTextTests.cs`, `MultiplexedStreamAdapterTests.cs`, `ReadAheadConnectionStreamTests.cs`.

## Context

- Split from BL-1476 (one task per range of files, as its Notes direct); BL-1476 keeps the whole-project checks and depends on this task. Follow BL-1476's Context: what matters in this project (request line and headers, the scripted response, the parsed status, headers and body, each redirect or authentication step, the `CurlExitCode` with its error text), `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system. BL-1478 (`Curl.Protocol.Ldap.UnitTests`) shows the pattern.
- "These classes' filter" below is one `FullyQualifiedName~Curl.Protocol.Http.<Class>.` term per class the listed files declare, joined with `|`.

## Acceptance criteria

- [x] `dotnet test Curl.Protocol.Http.UnitTests --filter "<these classes' filter>" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Protocol.Http.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Http.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Protocol.Http.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts in the 12 files, before -> after: `Assert.` 191 -> 191, `[TestMethod` 106 -> 106, `[DataRow(` 161 -> 161.
- Detailed run with these classes' filter: 222 passed, 4 skipped (OS-conditioned twins), 222 `END` lines, none with arrange, act or assert 0.
- No test printed a `SLOW:` line, so no performance follow-up.
- Shared helpers in `HttpResponseHeadReaderTests` (`ReadEveryWayAsync`, `AssertStatus` and the like) became instance methods that write the lines; `HttpResponseBodyReaderTests.AssertReadFailsAsync` takes the `TestDiagnostics`. Where a test asserted on a call inline, the result is now held in a local first; the asserted values are unchanged.
- Choice: exception messages are printed only where they are Curl's own text; the peer-reset tests whose expected text comes from `SocketException.Message` print only whether it matched, so no output depends on the operating system.
- Full fast run of `Curl.Protocol.Http.UnitTests`: 1846 passed, 18 skipped, 0 failed; `dotnet build -warnaserror` clean.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. HttpResponseBodyFramingTests to ReadAheadConnectionStreamTests write ARRANGE, ACT and ASSERT diagnostics in all 106 test methods
