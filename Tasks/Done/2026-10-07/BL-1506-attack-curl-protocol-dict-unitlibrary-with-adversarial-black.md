---
id: BL-1506
title: Attack Curl.Protocol.Dict.UnitLibrary with adversarial black-box tests in Curl.Protocol.Dict.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1472]
touches: [Curl.Protocol.Dict.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1506 — Attack Curl.Protocol.Dict.UnitLibrary with adversarial black-box tests in Curl.Protocol.Dict.UnitTests

## Goal

`Curl.Protocol.Dict.UnitTests` gains adversarial black-box tests that attack `Curl.Protocol.Dict.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1472.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Protocol.Dict.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: DICT URLs (`d:`, `m:`, `find:` with missing, empty and extra parts, CR and LF in a word, very long words) and server replies with malformed, missing or out-of-range status codes and an unterminated text block.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Protocol.Dict.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Protocol.Dict.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Dict.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Protocol.Dict.UnitTests/` and this task file.

## Notes

New tests in `Curl.Protocol.Dict.UnitTests/DictProtocolHandlerAdversarialTests.cs`; the oracle is
`DictProtocolHandler`'s and `DictRequest`'s documented contract, which pins curl 8.21.0's measured
bytes. No new curl measurement was needed: every attacked path decodes into a case the existing
measured tests already fix (escape set, defaults, refusal of bytes below 0x20, pass-through reply).

- **Boundaries:** `ExecuteAsync_WordByteAtTheEscapeBoundaries_IsEscapedOnlyAtOrBelowSpace` (0x1F, 0x20,
  0x21, 0x7E), `ExecuteAsync_WordOf64KibLetters_IsSentWholeInOneCommandLine`,
  `ExecuteAsync_WordOf4096EncodedSpaces_SendsEverySpaceWithABackslash`,
  `ExecuteAsync_LookupWithOnlyColons_SendsTheDefaultsAndReportsTheWordMissing`,
  `ExecuteAsync_ReadFillingTheWholeReadBuffer_IsWrittenByteForByte` (16384 bytes),
  `ExecuteAsync_MaxFileSizeZeroOrNegative_IsNoLimit` (0, -1, long.MinValue),
  `ExecuteAsync_MaxFileSizeOfLongMaxValue_WritesTheWholeReplyWithoutOverflowing`,
  `ExecuteAsync_MaxFileSizeOfOneByte_WritesOneByteAndExits63`.
- **Malformed input:** `ExecuteAsync_CrLfOrTabInjectedInAnyField_IsRefusedWithExit3AndNothingReachesTheWire`
  (CR/LF/TAB in word, database, strategy and plain command),
  `ExecuteAsync_TruncatedOrBrokenPercentEscape_IsSentAsWrittenAndValidEscapesAreDecoded`,
  `ExecuteAsync_PrefixWrittenWithPercentEscapes_IsRecognisedAfterDecoding`,
  `ExecuteAsync_DelOrHighByteInAPlainCommand_IsSentUnescaped`,
  `ExecuteAsync_ReplyOfBinaryBytesIncludingNul_IsWrittenByteForByte`.
- **Invalid partitions:** `ExecuteAsync_MalformedMissingOrOutOfOrderStatus_IsWrittenUnalteredWithExit0`
  (999, 099, non-numeric, two-digit, code with no CRLF, empty lines, 552, unterminated text block,
  250 before 150), `ExecuteAsync_UnknownCommandInThePath_IsSentAsTheCommandWithoutJudgingIt`.
- **State and concurrency:** `ExecuteAsync_ReplyDeliveredOneBytePerRead_IsWrittenWholeAndCountedPerByte`,
  `ExecuteAsync_ReplySplitAtEveryOffset_IsAlwaysWrittenWhole`,
  `ExecuteAsync_OneHandlerRunTwice_GivesTheSecondTransferNothingFromTheFirst`,
  `ExecuteAsync_RefusedPathThenValidPath_OnOneHandler_SecondTransferSucceeds`,
  `ExecuteAsync_SixtyFourTransfersAtOnceOnOneHandler_EachSendsAndWritesOnlyItsOwnBytes`,
  `ExecuteAsync_CancelledDuringTheReplyRead_ThrowsOperationCanceledAndDisposesTheConnection`,
  `ExecuteAsync_TokenCancelledAfterTheTransferFinished_LeavesTheResultAndOutputAsTheyWere`.
  Time: not applicable - the handler sets no timeout of its own; `TimeProvider` only times the
  diagnostic log, which `DictProtocolHandlerDiagnosticLogTests` already drives.
- **Defects found:** none, so no follow-up task was filed.
- **Test count:** 89 before (85 passed, 4 skipped off-platform), 141 after (137 passed, 4 skipped).
  No input is over 1 MiB, so nothing is Integration. Library unchanged, so its coverage is unchanged.

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Curl.Protocol.Dict.UnitTests attacks DictProtocolHandler with 52 adversarial cases across all four families; no defects found
