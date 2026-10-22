---
id: BL-1513
title: Attack Curl.Protocol.Mqtt.UnitLibrary with adversarial black-box tests in Curl.Protocol.Mqtt.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1479]
touches: [Curl.Protocol.Mqtt.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1513 — Attack Curl.Protocol.Mqtt.UnitLibrary with adversarial black-box tests in Curl.Protocol.Mqtt.UnitTests

## Goal

`Curl.Protocol.Mqtt.UnitTests` gains adversarial black-box tests that attack `Curl.Protocol.Mqtt.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1479.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Protocol.Mqtt.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: remaining-length varints at 127, 128, 268435455 and an invalid fifth byte, topic names that are empty or contain `#`, `+` or NUL, QoS values outside 0-2, unexpected CONNACK return codes and packets cut short.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Protocol.Mqtt.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Protocol.Mqtt.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Mqtt.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Protocol.Mqtt.UnitTests/` and this task file.

## Notes

All new tests are in `Curl.Protocol.Mqtt.UnitTests/MqttProtocolHandlerAdversarialTests.cs`, driven through `ScriptedConnection` (no network). The oracle is curl 8.21.0's `lib/mqtt.c` (no new command-line output was pinned, so `Record-CurlExchange.ps1` was not needed): it decodes a remaining length without insisting on the shortest form, copies a PUBLISH body without parsing topic or QoS, and prints CONNACK bytes as lowercase `%02x`.

- **Boundaries** (applied): `ExecuteAsync_PublishRemainingLength127_FitsOneByteAndIsWrittenWhole`, `ExecuteAsync_PublishRemainingLength128_TakesTwoBytesAndIsWrittenWhole`, `ExecuteAsync_PublishRemainingLength268435455ClosedEarly_WritesWhatArrivedThenIsPartialFile` (streamed, never allocated whole), `ExecuteAsync_RemainingLengthFourContinuationBytesAndNoFifth_IsWeirdServerReply`, `ExecuteAsync_PublishWithFifthRemainingLengthByte_IsWeirdServerReplyAndWritesNothing`.
- **Malformed input** (applied): `ExecuteAsync_PublishRemainingLengthNotInShortestForm_IsDecodedAndWritten`, `ExecuteAsync_PublishTopicLengthPastItsBody_WritesTheBodyAsItIs`, `ExecuteAsync_ConnackHeaderThenClose_IsRecvError`, `ExecuteAsync_SubackTypeByteThenClose_IsWeirdServerReply`, `ExecuteAsync_TopicWithEncodedNul_SubscribesWithTheNulByte`.
- **Invalid partitions** (applied): `ExecuteAsync_ConnackReturnCodeOtherThanZero_IsWeirdServerReplyAndSubscribesNothing` (codes 01-04 and ff), `ExecuteAsync_PublishWithReservedQoS_WritesTheBodyAsItIs` (QoS 3, with and without DUP and RETAIN), `ExecuteAsync_TopicWithWildcard_SubscribesToItAsWritten` (`+`, `%23`, bare `#` as fragment), `ExecuteAsync_SubackBeforeConnack_IsTakenAsConnackOfWrongLength`.
- **State and concurrency** (applied): `ExecuteAsync_MeasuredExchangeOneBytePerRead_WritesTheSameBytesAndResult`, `ExecuteAsync_TwoByteRemainingLengthSplitAtEveryOffset_WritesThePublishWhole`, `ExecuteAsync_SameHandlerRunTwice_SecondRunMatchesTheFirst`, `ExecuteAsync_SixteenTransfersAtOnceOnOneHandler_EachGetsTheResultOfARunAlone`. Cancellation before the call was already covered by `ExecuteAsync_Cancelled_ThrowsAndDisposesTheConnection`; time is covered by the keep-alive tests.
- **Defects found**: none. All three first-run failures were mistakes in the new tests (two PUBLISH lengths off by one; CONNACK hex expected uppercase where curl prints lowercase), corrected against curl's source; no follow-up task was needed.
- **Test count**: 131 before, 156 after (`dotnet test Curl.Protocol.Mqtt.UnitTests --filter "TestCategory!=Integration"`: 151 passed, 5 skipped). `dotnet build Curl.Protocol.Mqtt.UnitTests -warnaserror` clean. No input is over 1 MiB, so there are no Integration tests.

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. 25 adversarial tests added across all four families; no defects found; 131 -> 156 tests, build and fast tests green
