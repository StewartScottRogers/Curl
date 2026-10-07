---
id: BL-1500
title: Attack Curl.Http3.UnitLibrary with adversarial black-box tests in Curl.Http3.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1466]
touches: [Curl.Http3.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1500 — Attack Curl.Http3.UnitLibrary with adversarial black-box tests in Curl.Http3.UnitTests

## Goal

`Curl.Http3.UnitTests` gains adversarial black-box tests that attack `Curl.Http3.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1466.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Http3.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: QPACK and HTTP/3 framing: variable-length integers at 2^62-1 and truncated; unknown and reserved (grease) frame types and settings; duplicate settings; QPACK references to evicted or future entries; blocked streams past the advertised limit; a `HEADERS` frame after `DATA` trailers.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Http3.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Http3.UnitTests -warnaserror` is clean and `dotnet test Curl.Http3.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Http3.UnitTests/` and this task file.

## Notes

Two new test classes in `Curl.Http3.UnitTests`, public surface only (no internal type is
touched by them), MSTest and the BCL only, no I/O beyond `MemoryStream`, no input over 1 MiB.
The oracle is RFC 9114, RFC 9204 and RFC 9000 section 16; curl shows none of this on its
command line, so nothing was measured with `Record-CurlExchange.ps1`.

- **Boundaries:** `ControlStream_GoawayNamingTheLargestClientBidirectionalStream_IsAccepted`,
  `ReadFrameAsync_UnknownTypeAtTheLargestVariableLengthInteger_IsSkipped`,
  `ReadFrameAsync_KnownTypeClaimingTheLargestLength_FailsAsExcessiveLoadBeforeReadingIt`,
  `ReadFrameAsync_PayloadAtAndOnePastTheLimit_IsReadThenRefused`,
  `ReadFrameAsync_SettingsWithTheLargestUnknownIdentifierAndValue_KeepsItAndDropsGrease`,
  `ReadFrameAsync_IntegersInLongerEncodingsThanNeeded_AreAccepted`,
  `ReadFrameOrDataAsync_DataClaimingTheLargestLengthThenEnding_GivesItsBytesThenFrameError`,
  `TryDecodeFieldSection_LastStaticIndexAndEmptyStrings_Decode`,
  `ReadEncoderStream_EntryExactlyTheCapacity_IsInsertedAndTheCapacityItsMaximum`.
- **Malformed input:** `ReadFrameAsync_PayloadNotLaidOutAsItsTypeRequires_FailsAsFrameError`,
  `ReadFrameAsync_UnknownTypeClaimingTheLargestLengthThenEnding_FailsAsFrameError`,
  `ReadFrameAsync_StreamCutAtEveryOffsetOfAFrame_EndsCleanlyOnlyAtTheStart`,
  `ReadFrameAsync_ForbiddenSettings_FailAsSettingsError` (duplicate known and grease
  identifiers, HTTP/2 settings, 0-or-1 settings out of range),
  `TryDecodeFieldSection_MalformedSection_FailsAsDecompressionFailed`,
  `ReadEncoderStream_InvalidInstruction_FailsAsEncoderStreamError`,
  `ReadDecoderStream_InvalidInstruction_FailsAsDecoderStreamError`, and two seeded fuzzers
  (seed 1500): `ReadFrameAsync_SeededRandomBytes_ReturnFramesOrFailWithAnHttp3ErrorOnly`,
  `TryDecodeFieldSection_SeededRandomSections_DecodeBlockOrFailWithAQpackErrorOnly`,
  `ReadEncoderAndDecoderStreams_SeededRandomBytes_ApplyOrFailWithAQpackErrorOnly`.
- **Invalid partitions:** `ReadFrameAsync_ReservedHttp2TypeOfEmptyPayload_FailsAsFrameUnexpected`,
  `ControlStream_FrameNotAllowedAfterSettings_FailsAsFrameUnexpected`,
  `ControlStream_FrameInTheWrongState_FailsWithItsConnectionError`,
  `TryDecodeFieldSection_ReferenceToAnEvictedEntry_FailsAsDecompressionFailed`,
  `ReadEncoderStream_ReferenceToAnEvictedOrMissingEntry_FailsAsEncoderStreamError`,
  `TryDecodeFieldSection_ReferenceToAnEntryNotYetInserted_FailsAsDecompressionFailed`,
  `AcceptUnidirectionalStream_UnknownGreaseOrCutType_IsIgnored`,
  `AcceptUnidirectionalStream_PushStream_FailsAsIdError`,
  `PeerQpackStreams_EmptyBufferAndClosedStreams_AreRefused`.
- **State and concurrency:** `ControlStream_RepeatedAndLoweredGoaway_KeepsTheLatest`,
  `ControlStream_ReadAgainAfterItClosed_FailsAsClosedCriticalStreamEachTime`,
  `ReadFrameAsync_OneBytePerRead_GivesTheSameFramesAsWholeReads`,
  `ReadFrameAsync_CancelledBeforeTheCall_ThrowsOperationCanceled`,
  `ReadFrameOrDataAsync_HeadersAfterTrailers_IsHandedOnForTheCallerToRefuse` (the reader
  leaves frame order to its caller, as its remarks say; no request-stream state machine
  exists in this library to attack),
  `AcceptUnidirectionalStream_EachCriticalTypeInterleavedWithGreaseThenRepeated_FailsOnTheSecond`,
  `TryDecodeFieldSection_BlockedStreamsPastTheLimitThenCancelledAndUnblocked_KeepsCountAndAcknowledges`,
  `ReadEncoderStream_IntegerThatNeverEnds_IsRefusedOnce62BitsArePassed`,
  `ReadEncoderStream_OneBytePerCall_InsertsOnlyWhenTheLastByteArrives`,
  `ReadDecoderStream_SectionAcknowledgedTwiceAndUnknownStreamCancelled_RefusesOnlyTheSecondAcknowledgement`,
  `RoundTrip_SectionsDecodedBeforeTheirEncoderStreamAndOutOfOrder_GiveTheirFields`.
  Concurrency on many tasks: not applicable, no type in the library claims to be thread-safe;
  each reader, encoder and decoder belongs to one connection's single reader.
- **Defects found:** none. Every attack was refused with the error code the RFCs name, or
  accepted where the RFCs allow it; no follow-up task filed.
- **Test count:** 223 before, 327 after (`dotnet test Curl.Http3.UnitTests --filter
  "TestCategory!=Integration"`); `dotnet build Curl.Http3.UnitTests -warnaserror` clean,
  `dotnet format --verify-no-changes` clean.

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Curl.Http3.UnitTests attacks HTTP/3 framing and QPACK at their boundaries, with malformed input, invalid partitions and state stress; 104 new tests, no defects found
