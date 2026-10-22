---
id: BL-1516
title: Attack Curl.Protocol.Smb.UnitLibrary with adversarial black-box tests in Curl.Protocol.Smb.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1482]
touches: [Curl.Protocol.Smb.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1516 — Attack Curl.Protocol.Smb.UnitLibrary with adversarial black-box tests in Curl.Protocol.Smb.UnitTests

## Goal

`Curl.Protocol.Smb.UnitTests` gains adversarial black-box tests that attack `Curl.Protocol.Smb.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1482.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Protocol.Smb.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: NetBIOS and SMB2 headers that are truncated or carry wrong lengths, share and path parsing (empty share, `..`, backslashes, Unicode), credit and status-code edge values, and reads that return less than asked.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Protocol.Smb.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Protocol.Smb.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Smb.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Protocol.Smb.UnitTests/` and this task file.

## Notes

- All new tests are in `Curl.Protocol.Smb.UnitTests/SmbAdversarialTests.cs`. Test count: 150 before (140 passed, 10 skipped by OS), 201 after (191 passed, 10 skipped), fast filter.
- Surface choice: the library's only public type is `SmbProtocolHandler`; its parsers are `internal` and already tested directly through `InternalsVisibleTo` by every existing test class. The attacks drive the handler through injected fakes (`ScriptedConnection`, a queue `IConnector`) where the answer is a transfer result, and the parsers directly where the oracle is curl 8.21.0's `smb.c` contract (a named refusal, never an exception). No real network; nothing over 1 MiB, so no Integration test.
- Boundaries (applied): `NegotiateResponse_LengthAroundTheChallengesEnd_IsReadOnlyWhenWhole` (80/81), `OpenResponse_LengthAroundItsFixedSize_IsReadOnlyWhenWhole` (99/100), `OpenResponse_ExtremeLastChangeTime_IsClampedNotThrown` (long.Min/Max, 0, -1), `WriteResponse_LengthAroundTheCountWord_IsReadOnlyWhenWhole` (41/43), `ReadResponse_LengthAroundTheDataOffsetWord_IsRefusedAsShortOrAsDataPastTheEnd` (49/51), `ReadResponse_DataEndingAtOrOnePastTheMessage_IsTakenOrRefused`, `ReadResponse_LargestDataOffsetAndLength_IsRefusedWithoutOverflow`, `ReceiveAsync_LargestNetBiosLength_IsRefusedAsTooLarge`, `ReceiveAsync_FrameOfExactlyTheMaximum_IsAccepted`, `ExecuteAsync_ReadOfExactlyTheMaximumPayload_AsksAgainAndEndsOnTheEmptyRead`, `ExecuteAsync_ReadOneShortOfTheMaximumPayload_EndsWithoutAskingAgain` (reads that return less than asked).
- Malformed input (applied): `ReadResponse_NonZeroStatus_IsRefusedEvenWithValidData`, `ReadResponse_DataOffsetInsideTheHeader_NeverThrows`, `ReceiveAsync_ByteCountOnePastTheFrame_IsRefused`, `ReceiveAsync_WordCountPastTheFrame_IsAcceptedWithoutReadingPastIt`, `ExecuteAsync_NegativeEndOfFile_Exits8`, `ExecuteAsync_ReadDataPastTheMessage_Exits56InvalidInputPacket`, `ExecuteAsync_TruncatedNegotiateResponse_Exits7`.
- Invalid partitions (applied): `UrlPath_InvalidPartition_IsRefusedWithCurlsText` (no share, backslash-only share, `%00`, `%1f`, tab), `UrlPath_EdgePartition_SplitsAsCurlWithoutNormalising` (empty file, empty share via `%2F`, `..` kept verbatim as curl does, backslash in share, `%7F`, lone `%`, Unicode and escaped UTF-8), `UrlPath_InvalidUtf8Escape_KeepsTheRawBytes`, `Identity_SeparatorPartitions_SplitAsCurl` (empty, lone separators, `/` searched before `\` as curl's `strchr` order). Credit fields do not apply: this is SMB1 (curl's only SMB), which has no credits; status-code edges are covered by the non-zero-status rows.
- State and concurrency (applied): `ReceiveAsync_ThreeMessagesByteByByte_EachArrivesWhole`, `ExecuteAsync_SameHandlerTwiceInARow_GivesTheSameResultBothTimes`, `ExecuteAsync_EightConcurrentTransfersOnOneHandler_EachWritesItsOwnFile`.
- Defect found, filed as BL-1665 (High, crash): `SmbReadResponse.TryGetData` throws `ArgumentOutOfRangeException` on a 50-byte read reply and `SmbWriteResponse.TryReadCount` on a 42-byte write reply - each minimum length is one byte short of the word it reads, and `SmbMessageReader` passes both frames. The passing sides of each boundary are pinned here; the failing rows land with the fix.
- Oracle: no new real-curl measurement; every expected refusal text and exit code is one the suite already pins from recorded curl exchanges (`SmbRecordedExchange`, `SmbMessages`), or curl's `smb.c` parsing order.
## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Curl.Protocol.Smb.UnitTests attacks the SMB parsers and handler in all four families (51 new tests); the off-by-one crash it found is BL-1665
