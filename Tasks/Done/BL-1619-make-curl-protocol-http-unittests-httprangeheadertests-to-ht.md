---
id: BL-1619
title: Make Curl.Protocol.Http.UnitTests' HttpRangeHeaderTests to HttpRequestHeadFormatterTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1619 — Make Curl.Protocol.Http.UnitTests' HttpRangeHeaderTests to HttpRequestHeadFormatterTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Protocol.Http.UnitTests` files (8 files, 128 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `HttpRangeHeaderTests.cs`, `HttpRedirectLocationTests.cs`, `HttpRequestBodyWriterTests.cs`, `HttpRequestFramingTests.cs`, `HttpRequestHeadFormatterTests.AltSvc.cs`, `HttpRequestHeadFormatterTests.H2cUpgrade.cs`, `HttpRequestHeadFormatterTests.TrEncoding.cs`, `HttpRequestHeadFormatterTests.cs`.

## Context

- Split from BL-1476 (one task per range of files, as its Notes direct); BL-1476 keeps the whole-project checks and depends on this task. Follow BL-1476's Context: what matters in this project (request line and headers, the scripted response, the parsed status, headers and body, each redirect or authentication step, the `CurlExitCode` with its error text), `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system. BL-1478 (`Curl.Protocol.Ldap.UnitTests`) shows the pattern.
- "These classes' filter" below is one `FullyQualifiedName~Curl.Protocol.Http.<Class>.` term per class the listed files declare (the partial `HttpRequestHeadFormatterTests`'s four files count once), joined with `|`.

## Acceptance criteria

- [x] `dotnet test Curl.Protocol.Http.UnitTests --filter "<these classes' filter>" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Protocol.Http.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Http.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Protocol.Http.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Approach (2026-10-07): each class gets `TestContext` and a `Diagnostics` property, and its
  shared helpers write the lines, so no assertion changed: `HttpRangeHeaderTests` and
  `HttpRedirectLocationTests` route the call through a helper that writes ARRANGE, ACT and
  ASSERT and returns the value the unchanged `Assert` checks; `HttpRequestHeadFormatterTests`
  (all four partial files) writes the URL and options through `ArrangedUrl`/`Arranged`, and
  `ExpectedHead` writes the request line, the head's `BYTES` and a `DIFF` before the unchanged
  `Assert.AreEqual`; `HttpRequestFramingTests` builds every framing through `FramingOf`, which
  writes the inputs and the whole framing, and each `Assert` has a `Diagnostics.Assert` beside
  it; `HttpRequestBodyWriterTests` writes the writer's settings, the body, the bytes written
  and sent, and a `DIFF` of every expected trace against the actual one.
- New file `Curl.Protocol.Http.UnitTests/HttpRequestOptionsDescription.cs` describes the
  options that differ from their defaults, shared by the formatter and framing tests.
- Six one-line tests whose single `Assert` held the call inline now take the result into a
  local first (`HttpRequestFramingTests`), and
  `HttpRequestBodyWriterTests.WriteAsync_StreamThrowsSomethingElse_LetsItThrough` keeps the
  exception `ThrowsExactlyAsync` returns to print its message; no check was added or removed.
- Counts (`Assert.` / `[TestMethod` / `[DataRow(`), before = after for every file:
  HttpRangeHeaderTests 6/6/13; HttpRedirectLocationTests 7/7/45; HttpRequestBodyWriterTests
  68/34/3; HttpRequestFramingTests 72/26/24; HttpRequestHeadFormatterTests.AltSvc 3/3/0;
  .H2cUpgrade 4/4/5; .TrEncoding 2/7/25; HttpRequestHeadFormatterTests 16/41/86.
- Filtered detailed run: 294 tests, 293 passed, 1 skipped
  (`Format_Compressed_OnTheOpenSslBuild_SendsTheSameFourTokens`, excluded on Windows); 293
  `END` lines, none with a zero count. Whole project fast run: 1846 passed, 18 skipped.
- No test printed a `SLOW:` line.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. All 128 test methods in the eight HttpRangeHeaderTests to HttpRequestHeadFormatterTests files write ARRANGE, ACT and ASSERT diagnostics
