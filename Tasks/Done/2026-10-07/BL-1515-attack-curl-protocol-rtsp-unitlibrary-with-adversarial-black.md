---
id: BL-1515
title: Attack Curl.Protocol.Rtsp.UnitLibrary with adversarial black-box tests in Curl.Protocol.Rtsp.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1481]
touches: [Curl.Protocol.Rtsp.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1515 — Attack Curl.Protocol.Rtsp.UnitLibrary with adversarial black-box tests in Curl.Protocol.Rtsp.UnitTests

## Goal

`Curl.Protocol.Rtsp.UnitTests` gains adversarial black-box tests that attack `Curl.Protocol.Rtsp.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1481.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Protocol.Rtsp.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: CSeq mismatches and missing CSeq, interleaved `$` frames with bad channel numbers and lengths, session IDs that change, every request method, and response headers that are malformed or oversized.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Protocol.Rtsp.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Protocol.Rtsp.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Rtsp.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Protocol.Rtsp.UnitTests/` and this task file.

## Notes

All new tests are in `Curl.Protocol.Rtsp.UnitTests/RtspProtocolHandlerAdversarialTests.cs` and drive only the public `RtspProtocolHandler` through `ScriptedConnection` and two private fakes (a queued connector, a cancelling connection). Oracle: the handler's documented contract (ADR-0169, measured against curl 8.21.0), plus new curl measurements for interleaved frames.

- **Boundaries:** `ExecuteAsync_ReplyCSeqZeroOnAFreshConnection_FailsWith85NamingZero`, `ExecuteAsync_ReusedConnectionAnsweredWithCSeqZero_Succeeds`, `ExecuteAsync_ReusedConnectionAnsweredWithCSeqOne_FailsWith85`, `ExecuteAsync_ReplyCSeqAtLongMaxValue_FailsWith85NamingIt`, `ExecuteAsync_StatusAtEachBoundaryWithoutFail_EndsWithItsExitCode` (099, 100, 399, 400, 999), `ExecuteAsync_StatusAtEachBoundaryUnderFail_EndsWithItsExitCode` (399, 400, 999), `ExecuteAsync_ContentLengthZeroWithBytesAfterTheHead_ReadsNoBody`, `ExecuteAsync_ContentLengthAtLongMaxValueThenClose_SucceedsWithTheBytesReceived`, `ExecuteAsync_ContentLengthOnePastLongMaxValue_LeavesNoBodyAndDoesNotKeepTheConnection`, `ExecuteAsync_ContentLengthAroundMaxFileSize_FailsOnlyOverTheLimit` (4, 5, 6 against 5), `ExecuteAsync_HeadOfExactlyTheLimit_IsReadWhole` (102400 bytes), `ExecuteAsync_HeadOneByteOverTheLimit_FailsWith100`, `ExecuteAsync_ManyShortHeaderLinesUnderTheLimit_AreReadWhole`.
- **Malformed input:** `ExecuteAsync_MalformedHeaderLine_FailsWith8` (no colon, bare CR, NUL, `Content-Length` lists and repeats that disagree, negative length, two different `Location`s), `ExecuteAsync_RepeatedHeaderThatAgrees_Succeeds`, `ExecuteAsync_SessionIdChangingCaseOfTheHeaderName_FailsWith86`. Interleaved `$` frames before the reply: measured, defect filed (below), no test committed.
- **Invalid partitions:** `ExecuteAsync_EveryRequestMethod_StillSendsOptionsStar` - every RTSP method plus an empty and a two-word method through `-X`, each still sending `OPTIONS *` with `CSeq: 1`. Bad interleaved channel numbers and lengths are part of the BL-1663 defect, since no `$` frame is accepted at all yet.
- **State and concurrency:** `ExecuteAsync_ReplyWithBodySplitInTwoAtEveryOffset_ReadsTheSameHeadAndBody`, `ExecuteAsync_ReplyCutAtEveryOffset_NeverThrows`, `ExecuteAsync_SameHandlerForTwoTransfers_StartsEachAtCSeqOne` (a changed session ID across transfers is not carried over), `ExecuteAsync_SixteenTransfersAtOnceOnOneHandler_AllMatchTheirOwnReplies`, `ExecuteAsync_CancelledWhileTheReplyIsAwaited_ThrowsAndDisposesTheConnection`. Time: not applicable - the handler reads `TimeProvider` only to log elapsed time and has no timeout of its own.
- **Defects filed:** BL-1663 (Normal): a reply starting with an interleaved `$` RTP frame or stray bytes before `RTSP/1.0` fails with 52; curl 8.21.0 skips them and exits 0 (or 85 on the CSeq that follows), measured with `Record-CurlExchange.ps1` on 2026-10-07.
- **Test count:** 219 before (213 passed, 6 skipped off-platform), 269 after (263 passed, 6 skipped). `dotnet build Curl.Protocol.Rtsp.UnitTests -warnaserror`: 0 warnings, 0 errors. No input over 1 MiB, so no Integration test.

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. RTSP handler attacked on CSeq, status, length, head-size, malformed-header, method, split-reply and concurrency fronts; 50 new tests; interleaved-frame defect filed as BL-1663
