---
id: BL-1509
title: Attack Curl.Protocol.Gopher.UnitLibrary with adversarial black-box tests in Curl.Protocol.Gopher.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1475]
touches: [Curl.Protocol.Gopher.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1509 — Attack Curl.Protocol.Gopher.UnitLibrary with adversarial black-box tests in Curl.Protocol.Gopher.UnitTests

## Goal

`Curl.Protocol.Gopher.UnitTests` gains adversarial black-box tests that attack `Curl.Protocol.Gopher.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1475.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Protocol.Gopher.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: selectors containing tabs, CR and LF, every item-type character and an unknown one, an empty selector, very long selectors, and `gophers://` handshakes that fail.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Protocol.Gopher.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Protocol.Gopher.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Gopher.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Protocol.Gopher.UnitTests/` and this task file.

## Notes

All new tests are in `Curl.Protocol.Gopher.UnitTests/GopherProtocolHandlerAdversarialTests.cs`.
Every selector pinned was measured against curl 8.21.0 (Schannel, Windows) with
`Record-CurlExchange.ps1` on 2026-10-07 (e.g. `/1a%0D%0Ab` sent `61 0D 0A 62 0D 0A`,
`/%00foo` sent `00foo`, `/1%2500` sent `%00`, `/1a%00` sent nothing and exited 3,
`GOPHER://` served as `gopher://`).

- **Boundaries:** `ExecuteAsync_PathOfThreeBytes_SendsTheOneByteAfterTheItemType`,
  `ExecuteAsync_EscapeAtTheEndOfThePath_IsDecodedOnlyWhenTwoHexDigitsFollow` (3 rows),
  `ExecuteAsync_SelectorOf64KibBytes_IsSentWholeBeforeItsCrlf`,
  `ExecuteAsync_SelectorOf4096EncodedTabs_SendsEveryTabDecoded`,
  `ExecuteAsync_ReplyAroundTheReadBufferSize_ReachesOutputWhole` (16383/16384/16385),
  `ExecuteAsync_MaxFileSizeAroundTheReplyLength_CutsOnlyPastTheLimit` (5 rows, incl. 0 and -1),
  `ExecuteAsync_PortAtItsLimits_ConnectsToThatPort` (5 rows).
- **Malformed input:** `ExecuteAsync_EncodedCrAndLfInTheSelector_AreSentDecodedAsCurlSendsThem` (3 rows),
  `ExecuteAsync_SeveralEncodedTabs_SendsEachTabDecoded`,
  `ExecuteAsync_EncodedNulInTheItemTypePosition_IsRemovedBeforeDecodingSoNothingIsRefused`,
  `ExecuteAsync_EncodedPercentBeforeZeros_DecodesOnceAndSendsALiteralEscape`,
  `ExecuteAsync_EncodedNulAsTheLastByte_ReturnsUrlMalformatAndSendsNothing`,
  `ExecuteAsync_EncodedBytesAbove0x7F_AreSentAsRawBytes`,
  `ExecuteAsync_EncodedDotsAfterTheItemType_AreNotDotSegments`,
  `ExecuteAsync_ReplyOfEveryByteValueWithNoTerminatorLine_ReachesOutputUnaltered`.
- **Invalid partitions:** `ExecuteAsync_AnyItemTypeCharacterKnownOrNot_IsRemovedWithoutBeingInterpreted`
  (18 known item types, 3 unknown), `ExecuteAsync_SchemeInUpperOrMixedCase_IsServedAsItsLowerCaseScheme` (2 rows),
  `ExecuteAsync_GophersHandshakeFails_ReturnsTheConnectorsExit35UnchangedAndReportsNothing`,
  `ExecuteAsync_GophersEndsWithoutCloseNotifyAfterPartialReply_KeepsTheBytesAndReturnsExit56`.
- **State and concurrency:** `ExecuteAsync_ReplyDeliveredOneBytePerRead_ReachesOutputWholeWithOneDownloadReportPerByte`,
  `ExecuteAsync_ZeroByteReadBeforeMoreData_EndsTheTransferThereAndReadsNoFurther`,
  `ExecuteAsync_CancelledWhileTheReplyIsBeingWritten_ThrowsAndDisposesTheConnection`,
  `ExecuteAsync_TokenCancelledAfterTheTransferFinished_LeavesItsResultUntouched`,
  `ExecuteAsync_SameHandlerAfterARefusedSelector_ServesTheNextTransferNormally`,
  `ExecuteAsync_SixtyFourConcurrentTransfersOnOneHandler_EachSendsItsOwnSelectorAndGetsTheWholeReply`.
  The handler holds no time-dependent state of its own, so no `TimeProvider` attack applies.

Defects found: none; every attack matched measured curl or the documented contract, so no
follow-up task was filed. No input exceeds 1 MiB, so no test is in `Integration`.

Test count (`--filter "TestCategory!=Integration"`): 87 before (82 passed, 5 skipped),
147 after (142 passed, 5 skipped). `dotnet build Curl.Protocol.Gopher.UnitTests -warnaserror`
clean; `dotnet format --verify-no-changes` clean for the new file.

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Curl.Protocol.Gopher.UnitTests attacks the gopher handler with 60 adversarial black-box tests across all four families; no defects found
