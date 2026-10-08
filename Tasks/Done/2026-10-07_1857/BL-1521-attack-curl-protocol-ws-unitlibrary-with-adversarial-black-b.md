---
id: BL-1521
title: Attack Curl.Protocol.Ws.UnitLibrary with adversarial black-box tests in Curl.Protocol.Ws.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1487]
touches: [Curl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1521 — Attack Curl.Protocol.Ws.UnitLibrary with adversarial black-box tests in Curl.Protocol.Ws.UnitTests

## Goal

`Curl.Protocol.Ws.UnitTests` gains adversarial black-box tests that attack `Curl.Protocol.Ws.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1487.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Protocol.Ws.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: frame headers with payload lengths 125, 126, 127, 65535, 65536 and 2^63, mask rules broken in each direction, control frames past 125 bytes or fragmented, invalid close codes (999, 1004, 1005, 1006, 5000), text frames with invalid UTF-8 split across fragments, and a handshake whose `Sec-WebSocket-Accept` is wrong.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Protocol.Ws.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Protocol.Ws.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Ws.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Protocol.Ws.UnitTests/` and this task file.

## Notes

- All attacks go through the public `WsProtocolHandler.ExecuteAsync` with the existing
  `ScriptedConnection` fake; the new tests are in `WsProtocolHandlerAdversarialTests.cs`, plus a
  `Fakes/FreshConnectionConnector.cs` (a new connection per connect, for the concurrency attack).
- Oracle: real curl 8.21.0 (Schannel, mingw64), measured 2026-10-07 with `Record-CurlExchange.ps1`
  against the same 101 and frames, for every exit code, message and output byte pinned. One
  measurement is not pinned as curl printed it: a ping between fragments ended curl's run with
  `(56) Recv failure: Connection was reset`, because the recording server had already closed when
  curl sent its pong; the fake keeps the connection open, so the test pins the protocol answer
  (output `ab`, an empty pong sent, exit 0).
- **Boundaries:** `ExecuteAsync_BinaryFrameAtALengthFormBoundary_WritesTheWholePayload` (0, 125, 126,
  65535, 65536), `ExecuteAsync_64BitLengthWithItsTopBitSet_FailsWith56AsCurlDoes` (2^63),
  `ExecuteAsync_LongestLegal64BitLengthThenTheServerCloses_SucceedsWithWhatArrived` (2^63-1),
  `ExecuteAsync_PingOfExactly125Bytes_EchoesItInAPong`,
  `ExecuteAsync_ControlFramePast125Bytes_FailsWith56NamingTheFrame` (ping, close and pong of 126; a
  ping in the 64-bit form).
- **Malformed input:** `ExecuteAsync_MaskedServerFrame_FailsWith56AndWritesNothing`,
  `ExecuteAsync_ShortPayloadInALongerLengthFormThanNeeded_IsAcceptedAsCurlDoes`,
  `ExecuteAsync_101WithAMalformedAccept_StillSucceedsAsCurlDoes` (an empty accept, one character too
  long, the wrong case, two accepts), `ExecuteAsync_TextWithInvalidUtf8SplitAcrossFragments_WritesTheBytesUnchanged`,
  `ExecuteAsync_CloseWithAOneBytePayload_SucceedsWritingTheByteWithoutAWarning`,
  `ExecuteAsync_ServerClosesInsideAFrameHead_SucceedsWithWhatCameBefore`.
- **Invalid partitions:** `ExecuteAsync_ReservedBitOrUnknownOpcode_FailsWith56NamingTheFirstByte`
  (RSV1, RSV2, RSV3; opcodes 3, 7, B, F), `ExecuteAsync_FragmentedControlFrame_FailsWith56` (ping,
  close, pong without FIN), `ExecuteAsync_FrameOutOfMessageOrder_FailsWith56AfterWritingWhatCameBefore`
  (a continuation with no message or after a final frame, binary inside text, text inside binary),
  `ExecuteAsync_CloseWithAnInvalidCode_SucceedsWritingTheCodeAndWarnsOfIt` (999, 1004, 1005, 1006,
  5000: curl does not police close codes), `ExecuteAsync_TextFrameAfterTheClose_IsStillWrittenAsCurlDoes`.
- **State and concurrency:** `ExecuteAsync_EveryByteInItsOwnRead_MatchesTheWholeExchangeInOneRead`,
  `ExecuteAsync_ExchangeSplitAtEveryOffset_MatchesTheWholeExchange`,
  `ExecuteAsync_TooLongLengthSplitAtEveryOffset_FailsAlikeEveryTime`,
  `ExecuteAsync_RandomlyChunkedExchange_MatchesTheWholeExchange` (seed 1521, 50 rounds),
  `ExecuteAsync_PingBetweenFragments_WritesTheWholeMessageAndAnswersThePing`,
  `ExecuteAsync_TokenCancelledBeforeTheCall_ThrowsOperationCanceledAndSendsNothing`,
  `ExecuteAsync_TokenCancelledAfterTheTransfer_LeavesTheResultAlone`,
  `ExecuteAsync_SameHandlerAfterAViolation_RunsTheNextTransferCleanly`,
  `ExecuteAsync_ManyTransfersAtOnceOnOneHandler_EachMatchesASingleTransfer` (32 at once). Time is
  not attacked: the handler takes no timeout of its own (`-m` is the runner's cancellation, covered
  by the cancellation tests).
- No input exceeds 1 MiB (the largest is a 65536-byte payload), so no test is `Integration`.
- **Defects found:** none. Every attack matched curl 8.21.0 or the library's documented contract,
  so no follow-up task was filed.
- Test count: 355 before (350 passed, 5 skipped), 408 after (403 passed, 5 skipped): 53 new cases.
  `dotnet build Curl.Protocol.Ws.UnitTests -warnaserror`: 0 warnings, 0 errors. No library code
  changed, so the coverage gate is not re-measured.

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. 53 adversarial black-box WebSocket tests pinned to curl 8.21.0; no defects found
