---
id: BL-1613
title: Make Curl.Protocol.Http.UnitTests' HttpProtocolHandlerTests.AuthenticationHandshake to HttpProtocolHandlerTests.DiagnosticLog tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1613 — Make Curl.Protocol.Http.UnitTests' HttpProtocolHandlerTests.AuthenticationHandshake to HttpProtocolHandlerTests.DiagnosticLog tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Protocol.Http.UnitTests` files (9 files, 96 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `HttpProtocolHandlerTests.AuthenticationHandshake.cs`, `HttpProtocolHandlerTests.AuthorizationBesideHeader.cs`, `HttpProtocolHandlerTests.AwsSigV4.cs`, `HttpProtocolHandlerTests.Conditions.cs`, `HttpProtocolHandlerTests.ConnectionReuse.cs`, `HttpProtocolHandlerTests.ContentEncodingLimit.cs`, `HttpProtocolHandlerTests.ContentLengthOverflow.cs`, `HttpProtocolHandlerTests.Cookies.cs`, `HttpProtocolHandlerTests.DiagnosticLog.cs`.

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

- Counts per file (`Assert.` / `[TestMethod` / `[DataRow(`), identical before and after: AuthenticationHandshake 19/5/0, AuthorizationBesideHeader 6/3/0, AwsSigV4 22/8/2, Conditions 72/30/24, ConnectionReuse 53/18/9, ContentEncodingLimit 7/3/20, ContentLengthOverflow 17/5/3, Cookies 42/15/5, DiagnosticLog 21/9/0 (total 259/96/63).
- The class filter run printed 785 `END` lines, one per test run; every one of the 96 methods declared in these files printed `END` lines and none had arrange 0, act 0 or assert 0.
- No test printed a `SLOW:` line.
- Seven tests that discarded `ExecuteAsync`'s result now keep it in a `result` local so `WriteResult` can log it, and some inline `CollectionAssert` expected arrays became locals so `WriteExpectedLines` prints the same values. No assertion changed.
- Uses the shared `WriteResult`, `WriteEvents`, `WriteExpectedLines` and `OneLine` helpers BL-1612 added to `HttpProtocolHandlerTests.cs`; that file is unchanged.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. All 96 tests in the nine HttpProtocolHandlerTests files from AuthenticationHandshake to DiagnosticLog write ARRANGE, ACT and ASSERT diagnostics
