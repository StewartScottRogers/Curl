---
id: BL-1612
title: Make Curl.Protocol.Http.UnitTests' HttpExchangeLogTests to HttpProtocolHandlerTests.Authentication tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1612 — Make Curl.Protocol.Http.UnitTests' HttpExchangeLogTests to HttpProtocolHandlerTests.Authentication tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Protocol.Http.UnitTests` files (16 files, 95 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `HttpExchangeLogTests.cs`, `HttpFirstByteTimingConnectionTests.cs`, `HttpFrameLogTests.cs`, `HttpH2cUpgradeConnectionTests.cs`, `HttpInfoLineRecorderTests.cs`, `HttpLastModifiedTests.cs`, `HttpLineReaderTests.cs`, `HttpNegotiateInfoLinesTests.cs`, `HttpNtlmInfoLinesTests.cs`, `HttpPrefixedConnectionTests.cs`, `HttpPrefixedStreamTests.cs`, `HttpProtocolHandlerTests.AltSvc.cs`, `HttpProtocolHandlerTests.AuthProblemVerbose.cs`, `HttpProtocolHandlerTests.AuthRetryVerbose.cs`, `HttpProtocolHandlerTests.AuthUsingVerbose.cs`, `HttpProtocolHandlerTests.Authentication.cs`.

## Context

- Split from BL-1476 (one task per range of files, as its Notes direct); BL-1476 keeps the whole-project checks and depends on this task. Follow BL-1476's Context: what matters in this project (request line and headers, the scripted response, the parsed status, headers and body, each redirect or authentication step, the `CurlExitCode` with its error text), `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system. BL-1478 (`Curl.Protocol.Ldap.UnitTests`) shows the pattern.
- "These classes' filter" below is one `FullyQualifiedName~Curl.Protocol.Http.<Class>.` term per class the listed files declare, joined with `|`. `HttpProtocolHandlerTests` is one partial class (563 test methods) spread over seven tasks, BL-1612 to BL-1618; its term is `FullyQualifiedName~Curl.Protocol.Http.HttpProtocolHandlerTests.`, and the first criterion's check applies only to the `END` lines of its test methods declared in this task's files. A shared helper of that class (in `HttpProtocolHandlerTests.cs`) may write lines for the tests that use it.

## Acceptance criteria

- [x] `dotnet test Curl.Protocol.Http.UnitTests --filter "<these classes' filter>" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none for a test method declared in these files matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Protocol.Http.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Http.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Protocol.Http.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts in the 16 files, before -> after: `Assert.` 208 -> 208 (plus the new `Diagnostics.Assert` lines, which the pattern does not match), `[TestMethod` 95 -> 95, `[DataRow(` 59 -> 59. No assertion was removed or changed; a few `CollectionAssert` calls now take their expected lines from a local `expectedHead`/`expected` array so the ASSERT line can print the same lines first.
- `HttpProtocolHandlerTests.cs` gained the partial class's `TestContext`, `Diagnostics` and three shared writers for BL-1613 to BL-1618 to reuse: `WriteResult` (ACT exit code with number and error text), `WriteEvents` and `WriteExpectedLines` (event lines joined with ` | `, CR and LF shown as `\r` and `\n` by `OneLine` so each diagnostic stays one line). The file-local helpers `AuthProblemLinesAsync`, `DigestVerboseLinesAsync`, `AuthRetryEventsAsync`, `AuthUsingEventsOfUrlAsync` and `AssertReport` became instance methods and write the URL, scheme, exit code and events for every test that uses them.
- The filter run (`dotnet test ... --logger "console;verbosity=detailed"`, these classes' filter) ran 878 tests and printed 878 `END` lines; no test method declared in these files has a zero count. No test printed a `SLOW:` line, so no follow-up task.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every test in the 16 HttpExchangeLogTests-to-Authentication files writes ARRANGE, ACT and ASSERT diagnostics; no assertion changed
