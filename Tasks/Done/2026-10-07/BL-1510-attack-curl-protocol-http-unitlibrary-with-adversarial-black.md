---
id: BL-1510
title: Attack Curl.Protocol.Http.UnitLibrary with adversarial black-box tests in Curl.Protocol.Http.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1446, BL-1448, BL-1476]
touches: [Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1510 — Attack Curl.Protocol.Http.UnitLibrary with adversarial black-box tests in Curl.Protocol.Http.UnitTests

## Goal

`Curl.Protocol.Http.UnitTests` gains adversarial black-box tests that attack `Curl.Protocol.Http.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1446, BL-1448, BL-1476.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Protocol.Http.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: status lines (HTTP/0.9, 1.0 and 1.1, no reason phrase, codes 99, 100, 599, 600 and 999, garbage), header lines past curl's size limit, obsolete line folding, duplicate and conflicting `Content-Length`, `Content-Length` with chunked, chunk sizes that overflow or carry extensions, trailers, long runs of 1xx responses, CRLF injection through `-H` and `-A`, and responses that end early.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Protocol.Http.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Protocol.Http.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Http.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Protocol.Http.UnitTests/` and this task file.

## Notes

- Black box through `HttpProtocolHandler` only (the status-line, chunk and header parsers are `internal`), with `QueueConnector`/`ScriptedConnection` fakes, every response replayed with 1-byte and whole reads. All new tests are in `Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Adversarial.cs`. Every expected exit code, error text, body and request byte was measured 2026-10-07 with `Record-CurlExchange.ps1` against curl 8.21.0 (Schannel), `-sS -o <file>`.
- **Boundaries:** status codes 599, 600, 999 accepted, 99 and 20 refused, no reason phrase, `Content-Length` past `long.MaxValue` (curl accepts and reads to close), chunk sizes of 16 and 17 hex digits, thirty 1xx responses before the final one. Tests: `ExecuteAsync_AdversarialResponseCurlAccepts_DeliversTheBody`, `ExecuteAsync_StatusLineOutsideHttp1_FailsWithUnsupportedSubversion`, `ExecuteAsync_ChunkSizeCurlRefuses_FailsWithReceiveError`, `ExecuteAsync_ThirtyInterim100ResponsesBeforeTheFinalOne_DeliversTheBody`. Header lines past curl's 100 KiB limit are already pinned by `HttpProtocolHandlerTests.TooManyHeaders*.cs`, so not repeated.
- **Malformed input:** garbage status line, `2x0` code, HTTP/1.2, header with no colon, folding before any header, obsolete folding, bare LF line ends, chunk extension, trailer, non-hex chunk size, CRLF and bare LF in `-H` and `-A` (curl sends them verbatim). Tests: `ExecuteAsync_GarbageStatusLine_FailsAsHttp09NotAllowed`, `ExecuteAsync_HeaderWithoutColon_FailsWithWeirdServerReply`, `ExecuteAsync_LineBreakInACustomHeader_SendsItVerbatimAsCurlDoes`, `ExecuteAsync_CrlfInTheUserAgent_SendsItVerbatimAsCurlDoes`, plus rows of the accepting test above.
- **Invalid partitions:** conflicting and negative `Content-Length`, `Content-Length` beside chunked, only a 100 then close, a body shorter than its `Content-Length`, a chunk cut short. Tests: `ExecuteAsync_InvalidContentLength_FailsWithWeirdServerReply`, `ExecuteAsync_OnlyAnInterim100ThenClose_FailsWithEmptyReply`, `ExecuteAsync_BodyShorterThanContentLength_FailsWithPartialFileAfterDeliveringWhatCame`, `ExecuteAsync_ChunkCutShort_FailsWithPartialFileAfterDeliveringWhatCame`.
- **State and concurrency:** one handler fed refused and valid responses alternately, and one handler running sixteen transfers at once from the thread pool. Tests: `ExecuteAsync_OneHandlerAlternatingRefusedAndValidResponses_EachTransferStandsAlone`, `ExecuteAsync_OneHandlerSixteenConcurrentTransfers_EachGetsExactlyOneWholeBody`.
- **Defect found:** `HTTP/1.1 1000 X` - curl exits 8 `Invalid status line`, Curl exits 1 `Received HTTP/0.9 when not allowed`. Filed as BL-1662 (Normal: a wrong exit code, no crash, hang or security issue); its test was not committed here.
- No input over 1 MiB, so no Integration test. No platform-specific text or path.
- Test count (fast run, `Curl.Protocol.Http.UnitTests`): 1864 before, 1897 after (33 new cases).
## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. 33 adversarial black-box HTTP handler tests pinned to measured curl 8.21.0; one defect filed as BL-1662
