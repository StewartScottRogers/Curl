---
id: BL-1611
title: Make Curl.Protocol.Http.UnitTests' HttpChunkedDecoderTests to HttpDownloadConditionsTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1611 — Make Curl.Protocol.Http.UnitTests' HttpChunkedDecoderTests to HttpDownloadConditionsTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Protocol.Http.UnitTests` files (13 files, 97 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `HttpChunkedDecoderTests.cs`, `HttpConditionDateTests.cs`, `HttpConnectionPersistenceTests.cs`, `HttpConnectionStreamTests.cs`, `HttpContentChecksumTrailerTests.cs`, `HttpContentCodingDecoderTests.cs`, `HttpContentDecoderTests.cs`, `HttpContentInputTests.cs`, `HttpContentLengthTests.cs`, `HttpContentRangeTests.cs`, `HttpContinueWaitConnectionTests.cs`, `HttpDigestStaleChallengeTests.cs`, `HttpDownloadConditionsTests.cs`.

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

- Each of the 13 classes gets `TestContext` and a `Diagnostics` property (the BL-1478 pattern). Tests that loop over chunk sizes write their ACT and ASSERT lines per chunk size, labelled with it. Carriage returns and line feeds in scripted responses are written as backslash-r and backslash-n by a private `Visible` helper, so each diagnostic stays on one line; large bodies go through `Diff` rather than being printed.
- Counts in these 13 files, before -> after: `Assert.` 185 -> 185, `[TestMethod` 97 -> 97, `[DataRow(` 228 -> 228. No test logic or assertion changed; a few `Assert` arguments were first bound to a local so the same value is both written and asserted.
- Filtered detailed run: 279 tests, 279 `END` lines, none with a zero arrange, act or assert count.
- No test printed a `SLOW:` line; none of these tests has a `PHASE`.
- Only test files changed, so no library coverage changed and Measure-CodeQuality.ps1 was not needed.
- `dotnet build Curl.Protocol.Http.UnitTests -warnaserror` clean; fast tests 1846 passed, 18 skipped, 0 failed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every test in the 13 Http test files from HttpChunkedDecoderTests to HttpDownloadConditionsTests writes ARRANGE, ACT and ASSERT diagnostics
