---
id: BL-1621
title: Make Curl.Protocol.Http.UnitTests' AltSvcApplicationProtocolsTests to HttpAuthUsingLinesTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1621 — Make Curl.Protocol.Http.UnitTests' AltSvcApplicationProtocolsTests to HttpAuthUsingLinesTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Protocol.Http.UnitTests` files (15 files, 98 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `AltSvcApplicationProtocolsTests.cs`, `Fakes/FakeTimeProviderTests.cs`, `Fakes/QueueConnectorTests.cs`, `Fakes/ScriptedConnectionTests.cs`, `Fakes/TurnTakingConnectionTests.cs`, `Http2FrameTraceTests.cs`, `Http2RequestHeadersTests.cs`, `Http2ResponseHeadTests.cs`, `Http2SessionTests.cs`, `Http2StreamConnectionTests.cs`, `Http3SessionTests.cs`, `Http3StreamConnectionTests.cs`, `Http3StreamTraceTests.cs`, `HttpAuthProblemLinesTests.cs`, `HttpAuthUsingLinesTests.cs`.

## Context

- Split from BL-1476 (one task per range of files, as its Notes direct); BL-1476 keeps the whole-project checks and depends on this task. Follow BL-1476's Context: what matters in this project (request line and headers, the scripted response, the parsed status, headers and body, each redirect or authentication step, the `CurlExitCode` with its error text), `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system. BL-1478 (`Curl.Protocol.Ldap.UnitTests`) shows the pattern.
- "These classes' filter" below is one `FullyQualifiedName~<namespace>.<Class>.` term per class the listed files declare (namespace `Curl.Protocol.Http`, or `Curl.Protocol.Http.Fakes` under `Fakes/`), joined with `|`.

## Acceptance criteria

- [x] `dotnet test Curl.Protocol.Http.UnitTests --filter "<these classes' filter>" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Protocol.Http.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Http.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Protocol.Http.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts in the 15 files, before -> after: `Assert.` 209 -> 209, `[TestMethod` 98 -> 98, `[DataRow(` 74 -> 74 (diagnostic calls are `Diagnostics.Assert(`, which the `Assert.` count does not match).
- The filtered detailed run printed 174 `END` lines (98 methods with their data rows), none with an arrange, act or assert count of 0; all 174 passed.
- No test printed a `SLOW:` line, so no follow-up task.
- `dotnet build Curl.Protocol.Http.UnitTests -warnaserror` clean; the project's fast tests passed (1846 passed, 18 skipped); `dotnet format --verify-no-changes` clean.
- Choice: tests that only assert an exception keep the `Assert.Throws*` result in a local and write its type or message as the Act and Assert lines; byte payloads go through `BYTES`, head text through `DIFF`.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. AltSvcApplicationProtocolsTests to HttpAuthUsingLinesTests (15 files, 98 tests) write ARRANGE, ACT and ASSERT diagnostics
