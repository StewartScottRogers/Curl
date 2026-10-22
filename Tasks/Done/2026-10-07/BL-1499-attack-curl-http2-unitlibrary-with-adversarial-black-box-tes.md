---
id: BL-1499
title: Attack Curl.Http2.UnitLibrary with adversarial black-box tests in Curl.Http2.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1465]
touches: [Curl.Http2.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1499 — Attack Curl.Http2.UnitLibrary with adversarial black-box tests in Curl.Http2.UnitTests

## Goal

`Curl.Http2.UnitTests` gains adversarial black-box tests that attack `Curl.Http2.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1465.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Http2.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: frame and HPACK decoding: frame lengths at 16384, the peer's `SETTINGS_MAX_FRAME_SIZE` and one past; stream ID 0 where it is forbidden and even IDs from the server; HPACK integers that overflow, Huffman strings with bad padding, dynamic-table size updates out of order and beyond the limit; CONTINUATION floods; `WINDOW_UPDATE` of 0 and past 2^31-1; `RST_STREAM` and `GOAWAY` in the middle of a response.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Http2.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Http2.UnitTests -warnaserror` is clean and `dotnet test Curl.Http2.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Http2.UnitTests/` and this task file.

## Notes

Tests added in three new classes in `Curl.Http2.UnitTests`: `HpackAdversarialTests`, `Http2FrameAdversarialTests` and `Http2ConnectionAdversarialTests`. Public surface and the existing `PeerStream` fake only: no internal member, no reflection, no network, no input over 1 MiB (the largest is a 16385-byte frame), so no Integration test was needed. Seeds are fixed in the tests (1499, 14991, 7541) and appear in the assertion messages and diagnostics.

Test count (fast run): 230 before, 358 after.

**1. Boundaries** - applied.
- HPACK integers at 2^31 - 1 and one past: `Decode_IndexedFieldAtLargestInteger_RefusesAsInvalidIndex`, `Decode_IndexedFieldOnePastLargestInteger_RefusesAsIntegerOverflow`, `Decode_StringLengthAtLargestInteger_RefusesAsTruncatedBlock`, `Decode_TableSizeUpdateAtLargestInteger_RefusesAsTooLarge`.
- Indexes at both tables' edges: `Decode_IndexZero_RefusesAsInvalidIndex`, `Decode_LastStaticIndex_DecodesWwwAuthenticate`, `Decode_IndexOnePastStaticTableWithEmptyDynamicTable_RefusesAsInvalidIndex`, `Decode_LiteralNameIndexOnePastStaticTableWithEmptyDynamicTable_RefusesAsInvalidIndex`.
- Dynamic table sizes: `Decode_TableSizeUpdateExactlyAtAllowedMaximum_IsAccepted`, `Decode_TableSizeUpdateOnePastAllowedMaximum_RefusesAsTooLarge`, `Decode_LoweredLimitThenSizeUpdateExactlyToIt_DecodesAndShrinksTheTable`, `Decode_LoweredLimitThenSizeUpdateOnePastIt_RefusesAsTooLarge`, `Decode_IndexedLiteralLargerThanTheTable_EmptiesTheTableAndStillReturnsTheField`, `Decode_EmptyBlock_ReturnsNoFields`.
- Huffman padding: `HuffmanDecode_StringEndingInExactlySevenPaddingBits_Decodes`, `Decode_EmptyHuffmanValue_DecodesAsEmptyString`.
- Frame size 16384 and 16385: `ReadAsync_PayloadExactlyAtMaximumFrameSize_ReadsTheFrame`, `ReadAsync_PayloadOnePastMaximumFrameSize_RefusesWithFrameSizeError`, `ReadAsync_HeaderClaimingLargestLengthWithNoPayload_RefusesBeforeReadingThePayload`, `ReadStreamFrameAsync_DataExactlyAtTheMaximumFrameSize_ReturnsIt`, `ReadStreamFrameAsync_DataOnePastTheMaximumFrameSize_FailsWithFrameSizeError`.
- Fixed-length frames one byte short and one long (12 rows): `Parse_FixedLengthFrameOneByteShortOrLong_RefusesWithFrameSizeError`; padding: `ParseData_PadLengthOneBelowPayloadLength_ReturnsEmptyData`, `ParseData_PadLengthEqualToPayloadLength_RefusesWithProtocolError`.
- WINDOW_UPDATE of 0, of 2^31 - 1, and growing a window past 2^31 - 1: `ParseWindowUpdate_IncrementOfZeroOnceReservedBitIsIgnored_RefusesWithProtocolError`, `ParseWindowUpdate_LargestIncrement_ReturnsTwoToTheThirtyFirstMinusOne`, `ReadFrameAsync_ConnectionWindowUpdateExactlyToTheMaximum_IsAccepted`, `ReadFrameAsync_ConnectionWindowUpdateOnePastTheMaximum_FailsWithFlowControlErrorAndSendsGoAway`, `ReadFrameAsync_StreamWindowUpdatePastTheMaximum_FailsWithFlowControlError`, `ReadFrameAsync_WindowUpdateOfZero_FailsWithProtocolError`.
- SETTINGS limits: `SettingsApply_ValueJustOutsideItsRange_RefusesAndKeepsThePreviousValue`, `SettingsApply_ValuesExactlyAtTheirLimitsAndAnUnknownIdentifier_AreAccepted`, `ReadFrameAsync_SettingsWithExactlyTheMostEntries_IsAppliedAndAcknowledged`, `ReadFrameAsync_SettingsWithOneEntryTooMany_FailsWithEnhanceYourCalmBeforeApplyingAny`, `ReadFrameAsync_InitialWindowSizeOfZero_LeavesNothingToSend`, `ReadFrameAsync_MaxConcurrentStreamsOfZero_RefusesTheNextOpenStream`.
- CONTINUATION count at the limit and one past: `ReadStreamFrameAsync_HeaderBlockOfExactlyTheMostContinuationFrames_ReturnsTheJoinedBlock`, `ReadStreamFrameAsync_HeaderBlockOfOneContinuationFrameTooMany_FailsWithEnhanceYourCalm`.
- Flow-control window: `FlowControlWindow_GrownExactlyToTheMaximumThenByOne_RefusesTheSecondAndKeepsTheSize`, `FlowControlWindow_ConsumeExactlyItsSizeThenOneMore_EmptiesItThenRefuses`, `FlowControlWindow_DrivenNegative_ReportsNothingAvailableAndRefusesAnyConsumption`.

**2. Malformed input** - applied.
- HPACK: `Decode_IntegerPaddedWithSixZeroContinuationOctets_RefusesAsIntegerOverflow`, `Decode_BlockEndingInsideIntegerContinuation_RefusesAsTruncatedBlock`, `Decode_LiteralWithNameButNoValue_RefusesAsTruncatedBlock`, `Decode_HuffmanValueHoldingEndOfString_RefusesAsEndOfStringInData`, `HuffmanDecode_ThirtyOneOneBits_RefusesAsEndOfStringInData`, `Decode_HuffmanValuePaddedWithMoreThanSevenBits_RefusesAsInvalidPadding`, `Decode_HuffmanValuePaddedWithAZeroBit_RefusesAsInvalidPadding`, `NewDecoder_NegativeTableSize_Throws`, `NewEncoder_NegativeTableSize_Throws`.
- Seeded fuzzing, 2000 rounds each, only `HpackDecodingException` may escape: `Decode_EveryPrefixOfAValidBlock_DecodesOrRefusesWithHpackError`, `Decode_SeededSingleBitFlips_DecodeOrRefuseWithHpackError`, `Decode_SeededRandomBytes_DecodeOrRefuseWithHpackError`.
- Frames: `ReadAsync_ReservedStreamBitSet_IgnoresTheBit`, `ReadAsync_UnknownFrameType_ReadsItUnchanged`, `Parse_PaddedFlagWithEmptyPayload_RefusesWithProtocolError`, `ParseHeaders_PaddedPriorityFrameTooShortForPriorityOncePaddingIsRemoved_RefusesWithFrameSizeError`, `ParsePushPromise_PaddedFrameTooShortForPromisedStreamOncePaddingIsRemoved_RefusesWithFrameSizeError`, `ParseSettings_AcknowledgementWithOneEntry_RefusesWithFrameSizeError`, `ParseGoAway_ReservedBitInLastStream_IgnoresTheBit`, `Parse_NullFrame_ThrowsArgumentNull`.

**3. Invalid partitions** - applied.
- Wrong stream: `Parse_FrameOnTheWrongStream_RefusesWithProtocolError` (8 rows), `ReadFrameAsync_DataOnStreamZero_FailsWithProtocolError`, `ReadFrameAsync_HeadersOnAnIdleStream_FailsWithProtocolError` (server-chosen even stream, unopened odd stream), `ReadFrameAsync_WindowUpdateOnAnIdleStream_FailsWithProtocolError`, `ReadFrameAsync_RstStreamOnAnIdleStream_FailsWithProtocolError`.
- Wrong state: `ReadFrameAsync_ContinuationWithNoHeaderBlockOpen_FailsWithProtocolError`, `ReadStreamFrameAsync_FrameInterruptingAHeaderBlock_FailsWithProtocolError` (CONTINUATION on another stream, DATA, PING, SETTINGS), `ReadStreamFrameAsync_DataAfterTheStreamEnded_FailsWithStreamClosed`, `Decode_TableSizeUpdateAfterAField_RefusesAsNotAtStart`, `Decode_LoweredLimitThenBlockWithoutSizeUpdate_RefusesAsUpdateMissing`, `ReadFrameAsync_PushPromise_FailsWithProtocolError`, `ReadFrameAsync_PriorityOnWhichAStreamDependsOnItself_FailsWithProtocolError`.
- Out-of-range settings on the wire: `ReadFrameAsync_SettingOutsideItsRange_FailsTheConnection`, `ReadFrameAsync_InitialWindowSizeGrowingAStreamWindowPastTheMaximum_FailsWithFlowControlError`.
- Frames the endpoint must ignore: `ReadFrameAsync_FrameTheEndpointMayIgnore_IsIgnoredWithoutAnswer` (unknown type, PRIORITY on an idle stream), `ReadFrameAsync_PingAcknowledgement_IsNotAnswered`, `ReadFrameAsync_WindowUpdateOnAStreamThisEndpointReset_IsIgnored`.
- Every connection-error test also checks that the GOAWAY sent carries the same error code.

**4. State and concurrency** - applied for order, partial delivery and cancellation. Concurrency is not applicable: neither `Http2Connection` nor the HPACK coders claim to be safe for concurrent calls (a connection's frames are read in order). Time is not applicable: the library takes no `TimeProvider` and has no timeouts.
- RST_STREAM and GOAWAY in the middle of a response: `ReadStreamFrameAsync_RstStreamInTheMiddleOfAResponse_ThrowsStreamResetAndForgetsTheStream`, `ReadStreamFrameAsync_GoAwayLeavingTheOpenStreamUnprocessed_ThrowsGoAwayAndRefusesNewStreams`, `ReadStreamFrameAsync_GracefulGoAwayCoveringTheOpenStream_LetsTheResponseFinish`.
- CONTINUATION flood: `ReadStreamFrameAsync_EmptyContinuationFloodWithoutEndHeaders_FailsWithEnhanceYourCalm` (1000 empty CONTINUATION frames).
- Use after failure: `ReadFrameAsync_AfterAProtocolError_RefusesEveryFurtherCall`, `Decode_AfterARefusedBlock_DecodesTheNextBlockFromTheTableItHad`.
- Partial delivery: `ReadAsync_FrameCutAtEveryOffset_ThrowsEndOfStream`, `ReadAsync_OneBytePerRead_ReadsTheSameFrames`, `ReadStreamFrameAsync_WholeResponseOneBytePerRead_ReturnsTheSameStreamFrames`, `ReadStreamFrameAsync_ConnectionClosedInsideAHeaderBlock_ThrowsEndOfStream`.
- Cancellation: `ReadAsync_CancelledBeforeTheCall_ThrowsCancellation`, `ReadFrameAsync_CancelledBeforeTheCall_ThrowsCancellationAndTheNextReadStillWorks`.
- A long stateful run: `EncodeThenDecode_SeededHeaderListsWhileTheTableSizeChanges_RoundTripEveryBlock` (400 blocks, table size changed at random between 0 and 8192; the encoder's and decoder's tables stay in step).

**Oracle.** Everything here is below the command line, so the oracle is RFC 9113, RFC 7541 and each member's documented refusals. No curl measurement was needed.

**Defects found.** One: BL-1652 (Normal). `Http2FlowControlWindow.TryAdjust(long.MaxValue)` on a window of 1 overflows the `long` sum, returns `true` and leaves `Size` at `long.MinValue`, which breaks its doc comment. The wire cannot reach it (the connection passes only 31-bit deltas), so it is Normal. No failing or ignored test is committed for it; its test lands with the fix. The filed tasks (BL-1652 and BL-1653) are the only changes outside `Curl.Http2.UnitTests/` and this task, and they are committed separately.

**Harness finding.** BL-1653 (High). A test helper of mine recursed into itself and overflowed the stack. `dotnet test` still printed `Passed!` each time, with 224, 277 or 312 tests instead of 358, and only the normal-verbosity console logger showed `Stack overflow.`. The helper is fixed, and three runs in a row now give 358 out of the 358 that `--list-tests` lists. BL-1653 is to make a test host crash fail the run.

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Curl.Http2.UnitTests attacks HPACK, frames and the connection at every boundary with 128 adversarial tests; BL-1652 and BL-1653 filed
