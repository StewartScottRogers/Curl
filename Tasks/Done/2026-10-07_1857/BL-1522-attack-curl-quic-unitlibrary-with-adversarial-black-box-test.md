---
id: BL-1522
title: Attack Curl.Quic.UnitLibrary with adversarial black-box tests in Curl.Quic.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1488]
touches: [Curl.Quic.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1522 — Attack Curl.Quic.UnitLibrary with adversarial black-box tests in Curl.Quic.UnitTests

## Goal

`Curl.Quic.UnitTests` gains adversarial black-box tests that attack `Curl.Quic.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1488.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Quic.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: variable-length integers at 2^62-1 and truncated, long and short packet headers with connection IDs of 0, 20 and 21 bytes, ACK frames whose ranges underflow, unknown frame types, stream offsets near 2^62, flow-control limits exceeded by one byte, Retry and Version Negotiation packets, and coalesced packets.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Quic.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Quic.UnitTests -warnaserror` is clean and `dotnet test Curl.Quic.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Quic.UnitTests/` and this task file.

## Notes

All new tests are in `Curl.Quic.UnitTests/QuicAdversarialTests.cs`, one class like the
`*AdversarialTests.cs` files of the other projects. Oracle: RFC 9000 and 9001 and the
library's doc comments; QUIC framing has no command-line answer real curl could show for
these inputs, so nothing was measured with `Record-CurlExchange.ps1`. Every generator is a
`System.Random` with a fixed seed, printed with the input when a check fails.

- **Boundaries** - applied: `TryRead_EightByteEncodingOfTheMaximum_ReadsTwoToThe62MinusOne`,
  `TryRead_MaximumCutShortOfItsEightBytes_IsFalse`, `Write_TheMaximum_WritesEightBytesAndOneMoreThrows`,
  `Decode_StreamEndingExactlyAt2To62MinusOne_IsRead`, `Decode_StreamEndingOneBytePast2To62MinusOne_IsAFrameEncodingError`,
  `Decode_CryptoAtTheOffsetLimit_IsReadAndOneBytePastIsAFrameEncodingError`, `Decode_AckRangeReachingExactlyPacketZero_IsRead`,
  `Decode_AckRangeUnderflowingPacketZero_IsAFrameEncodingError`, `Decode_MaxStreamsAt2To60_IsReadAndOneMoreIsAFrameEncodingError`,
  `Decode_NewConnectionIdAtEachLengthLimit_IsRead`, `Decode_LongHeaderWithConnectionIdsAtEachLengthLimit_IsRead` (0 and 20 bytes),
  `Decode_ShortHeaderWithConnectionIdAtEachLengthLimit_IsRead` (0 and 20), `Decode_ShortHeaderWithA21ByteConnectionIdLength_Throws`,
  `Decode_VersionNegotiationAtItsLimits_ReadsEveryVersion`, `Decode_IntegerParameterExactlyAtItsLimit_IsRead`,
  `Receive_FrameEndingExactlyAtTheBufferLimit_IsHeldAndOneBytePastThrows`, `Decode_PacketNumberNearTheTwoTo62Ceiling_NeverPassesIt`.
  Flow-control limits exceeded by one byte are enforced by the internal `QuicReceiveCredit`,
  reachable publicly only through a whole `QuicConnection`, which `QuicClientStreamsTests`
  already drive past the limit; not repeated here.
- **Malformed input** - applied: `Decode_AckClaiming2To62MinusOneRangesWithNoneSent_IsAFrameEncodingErrorWithoutLooping`,
  `Decode_UnknownFrameType_IsAFrameEncodingError` (0x1f, DATAGRAM 0x30, two-, four- and eight-byte types),
  `Decode_NewConnectionIdOneFieldWrong_IsAFrameEncodingError`, `Decode_EveryProperPrefixOfAPayload_IsReadOrRefusedWithAFrameEncodingError`,
  `Decode_RandomFramePayloads_NeverThrowAnythingButATransportException` (20000 inputs),
  `Decode_EveryProperPrefixOfTheRfcClientInitial_IsAProtocolViolation`,
  `Decode_RetryWithExactlyAnIntegrityTagAfterItsConnectionIds_IsAProtocolViolation`, `Decode_RetryWithAOneByteToken_IsRead`,
  `HasValidTag_RfcRetryWithEachBitOfItsTagFlipped_IsFalse`, `HasValidTag_RfcRetryCheckedAgainstAnotherOriginalConnectionId_IsFalse`,
  `ComputeTag_A21ByteOriginalConnectionId_Throws`, `Decode_RandomDatagrams_NeverThrowAnythingButATransportException` (20000 inputs),
  `Decode_MalformedTransportParameters_IsATransportParameterError`,
  `Decode_RandomTransportParameters_NeverThrowAnythingButATransportException` (20000 inputs).
- **Invalid partitions** - applied: `Decode_FrameInAPacketTypeThatMayNotCarryIt_IsAProtocolViolation`
  (NEW_TOKEN in Handshake and 0-RTT, CRYPTO in 0-RTT, RESET_STREAM in Initial, MAX_DATA in
  Handshake, RETIRE_CONNECTION_ID in Initial), plus the above-range rows of the boundary tests
  (21-byte connection ID, stream count 2^60 + 1, data past 2^62 - 1, ACK ranges below packet 0).
- **State and concurrency** - applied: `Decode_CoalescedInitialHandshakeAndShortHeader_ReadsEachFromWhatIsLeft`,
  `Receive_OneByteFramesInReverseOrder_DeliversEverythingOnceAtTheEnd`,
  `Receive_OverlappingFramesInShuffledOrderWithRepeats_DeliversTheStreamExactlyOnce`,
  `Decode_SamePayloadOnManyTasksAtOnce_GivesTheSameFramesAsOneAfterAnother`, `Decode_AfterRefusingAPayload_ReadsTheNextOne`.
  Time and cancellation do not apply: the codecs and the reassembler take no `TimeProvider` or token.

Defects found: none, so no follow-up task was filed. Every attack was answered as RFC
9000/9001 and the doc comments say. The three first-run failures were mistakes in the tests
(a parameter ID 0 that was in fact well formed, Length 0x449e read as a number instead of
the varint 1182, and a CRYPTO generator that left gaps), corrected before commit.

Test count of `Curl.Quic.UnitTests` in the fast run: 411 before, 485 after.
`dotnet build Curl.Quic.UnitTests -warnaserror`: 0 errors; `dotnet format --verify-no-changes`
on the new file: clean. No input exceeds 1 MiB, so no test is in `TestCategory("Integration")`.

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Curl.Quic.UnitTests attacks the QUIC codecs, Retry integrity and CRYPTO reassembler at every RFC 9000 limit; 74 new tests, no defects found
