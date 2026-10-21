---
id: BL-1520
title: Attack Curl.Protocol.Tftp.UnitLibrary with adversarial black-box tests in Curl.Protocol.Tftp.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1452, BL-1486]
touches: [Curl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1520 — Attack Curl.Protocol.Tftp.UnitLibrary with adversarial black-box tests in Curl.Protocol.Tftp.UnitTests

## Goal

`Curl.Protocol.Tftp.UnitTests` gains adversarial black-box tests that attack `Curl.Protocol.Tftp.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1452, BL-1486.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Protocol.Tftp.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: block numbers wrapping at 65535, `blksize` at 7, 8, 65464 and 65465, duplicate and out-of-order ACKs and DATA, error packets with and without text, file names containing NUL, and timeouts at their boundaries through `TimeProvider`.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Protocol.Tftp.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Protocol.Tftp.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Tftp.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Protocol.Tftp.UnitTests/` and this task file.

## Notes

- New class `TftpProtocolHandlerAdversarialTests` (25 test cases, 15 methods) and the fake `Fakes/QueuedDatagramConnector` (one channel per open, safe across tasks). Oracle: curl 8.21.0's `tftp.c` as the existing suites already pin it; no new output text was pinned, so no new measurement was needed.
- **Boundaries:** `ExecuteAsync_BlockSize8DownloadPastBlock65535_WrapsToBlock0AndWritesEveryByteInOrder` (65536 blocks of 8 bytes, 512 KiB, under the 1 MiB line; ~0.2 s), `ExecuteAsync_BlockSize65464FullBlockThenEmptyBlock_TreatsTheFullBlockAsNotLast`, `ExecuteAsync_MaxFileSizeExactlyTheFirstBlockAndASecondBlockFollows_WritesTheFirstAndExits63`, `ExecuteAsync_ErrorPacketWithACodeCurlHasNoNameFor_ExitsAbortedByCallback` (codes 8 and 65535). `blksize` 7, 8, 65464 and 65465 were already pinned by `TftpProtocolHandlerTests`.
- **Malformed input:** `ExecuteAsync_ErrorPacketWithMalformedText_ExitsWithTheMappedCodeWithoutThrowing` (no text, empty, unterminated, non-UTF-8, NUL inside), `ExecuteAsync_OptionAcknowledgementWithAnOptionNameThatIsNotUtf8_IgnoresItAndKeeps512`, `ExecuteAsync_OptionAcknowledgementNamingBlksizeTwice_TakesTheLastOne`, `ExecuteAsync_OptionAcknowledgementBlksizeNotStartingWithADigitOrOverflowing_Exits71LargerThanMax` (+, -, space, overflow). File names with NUL were already pinned (`ExecuteAsync_DecodedNul_ReturnsExit3AndSendsNothing`).
- **Invalid partitions** (of the DATA block number): `ExecuteAsync_FirstReplyIsAnEmptyBlock1_AcksItAndEndsWritingNothing`, `ExecuteAsync_FirstReplyIsAShortBlock0_AcksBlock0AsARepeatAndEndsWritingNothing`, `ExecuteAsync_FirstReplyIsABlockNeitherExpectedNorRepeated_IgnoresItAndTakesBlock1` (65535, ahead, half way round).
- **State and concurrency:** `ExecuteAsync_SeededDuplicateAndOutOfOrderBlocks_WritesTheFileOnceInOrder` (seed 1520, printed), `ExecuteAsync_SameHandlerTwiceInARow_SecondTransferStartsFresh`, `ExecuteAsync_SameHandlerOnSixteenTransfersAtOnce_EachWritesItsOwnFile`, `ExecuteAsync_TokenCancelledBeforeTheCall_ThrowsOperationCanceled`. Timeouts at their boundaries through `TimeProvider` were already pinned in depth by `TftpDownloadRetransmissionTests` and `TftpUploadRetransmissionTests`, so none were added.
- **Defects found, filed, no test committed for them:** BL-1666 (an OACK after DATA does not restart the download at block 1, unlike curl's `tftp_rx`), BL-1667 (OACK option names are compared whole, curl uses `checkprefix`), BL-1668 (a DATA packet longer than the block size is written whole; curl truncates or fails by platform). All Normal: wrong bytes, no crash, hang or memory blow-up.
- Test count: 240 before, 265 after (`dotnet test Curl.Protocol.Tftp.UnitTests --filter "TestCategory!=Integration"`).

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Curl.Protocol.Tftp.UnitTests attacks the TFTP handler with 25 adversarial cases across all four families; defects filed as BL-1666, BL-1667, BL-1668
