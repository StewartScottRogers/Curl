---
id: BL-1503
title: Attack Curl.Ntlm.UnitLibrary with adversarial black-box tests in Curl.Ntlm.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1469]
touches: [Curl.Ntlm.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1503 — Attack Curl.Ntlm.UnitLibrary with adversarial black-box tests in Curl.Ntlm.UnitTests

## Goal

`Curl.Ntlm.UnitTests` gains adversarial black-box tests that attack `Curl.Ntlm.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1469.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Ntlm.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: NTLM Type 2 parsing: field offsets and lengths that point outside the message or overlap, target-info AV pairs that are malformed, duplicated or unterminated, Unicode and OEM flag combinations, zero-length fields, a message shorter than its header; Type 3 output for empty, very long and non-ASCII credentials.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Ntlm.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Ntlm.UnitTests -warnaserror` is clean and `dotnet test Curl.Ntlm.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Ntlm.UnitTests/` and this task file.

## Notes

Two new classes in `Curl.Ntlm.UnitTests`: `NtlmChallengeMessageAdversarialTests` (CHALLENGE and AV_PAIR parsing) and `NtlmAuthenticateMessageAdversarialTests` (AUTHENTICATE output, answerer, response computation). Public surface only, MSTest and the BCL, no I/O; largest input is 65 583 bytes, so nothing goes in `Integration`. Oracle: MS-NLMP 2.2 and the types' doc comments of curl 8.21.0's behaviour; nothing here is visible on the command line, so no `Record-CurlExchange.ps1` measurement was needed.

- **Boundaries:** `Decode_EmptyMessage_RefusesAsTooShort`, `Decode_OneByteShorterThanMinimum_RefusesAsTooShort`, `Decode_ExactlyMinimumLengthWithTargetInfoFlag_DecodesWithoutTargetInformation`, `Decode_OneByteShorterThanTargetInfoHeaderWithBogusTargetInfo_IgnoresTheTargetInfoField`, `Decode_TargetInfoEndingExactlyAtMessageEnd_ReadsIt`, `Decode_TargetInfoEndingOneBytePastMessageEnd_RefusesAsOutOfRange`, `Decode_TargetInfoStartingOneByteInsideHeader_RefusesAsOutOfRange`, `Decode_TargetInfoOffsetNearUInt32Max_RefusesWithoutOverflowing`, `Decode_TargetInfoOfMaximumLengthFillingTheMessage_ReadsAllOfIt`, `Decode_VersionFlagWithOneByteTooFewForVersion_LeavesVersionEmpty`, `Decode_VersionFlagWithExactlyEnoughForVersion_ReadsVersion`, `DecodeTargetInformation_ValueEndingExactlyAtTheEndWithNoEndOfList_RefusesAsUnterminated`, `DecodeTargetInformation_ValueLengthOfUInt16Max_RefusesAsTruncatedWithoutReadingPastTheEnd`, `TryEncode_MessageOneByteUnderCurlsBuffer_IsWritten`, `TryEncode_MessageExactlyCurlsBuffer_IsRefusedAsNamesTooLarge`, `TryEncode_ResponsesEndingExactlyAtCurlsBuffer_IsRefusedAsNamesTooLarge`, `TryEncode_ResponsesEndingOneBytePastCurlsBuffer_IsRefusedAsResponsesTooLarge`, `TryEncode_EmptyResponsesAndNames_WritesAHeaderOnlyMessage`, `Answer_EmptyUserAndPassword_EncodesEmptyDomainAndUserBuffers`, `Answer_VeryLongPassword_StillFitsCurlsBuffer`, `Answer_VeryLongUser_IsRefusedAsNamesTooLargeWithoutThrowing`, `Answer_TargetInformationFillingCurlsBuffer_IsRefusedAsResponsesTooLarge`, `ComputeLmOwfV1_PasswordsEqualInTheirFirst14Bytes_HashTheSame`, `ComputeLmOwfV1_PasswordsDifferingInTheirFourteenthByte_HashDifferently`.
- **Malformed input:** `Decode_SignatureOneByteWrong_RefusesAsWrongSignatureOrType`, `Decode_TargetInfoOverlappingItsOwnSecurityBuffer_RefusesAsOutOfRange`, `Decode_TargetNameAtAnyOffset_ReadsItOnlyWhenWhollyInsideTheMessage`, `Decode_TargetNamePointingIntoTheHeader_ReadsThoseBytesAsCurlDoes`, `Decode_SeededRandomMutationsOfAValidChallenge_NeverThrow` (seed 1503, 5000 mutations), `DecodeTargetInformation_Empty_RefusesAsUnterminated`, `DecodeTargetInformation_PairHeaderCutShort_RefusesAsTruncated`, `DecodeTargetInformation_ValueOneBytePastTheEnd_RefusesAsTruncated`, `DecodeTargetInformation_EndOfListWithANonZeroLengthAndNoValue_StopsThere`, `DecodeTargetInformation_GarbageAfterEndOfList_IsIgnored`, `DecodeTargetInformation_DuplicatedPairs_KeepsEveryOneInMessageOrder`, `DecodeTargetInformation_UnknownAvId_KeepsItAsItsNumber`, `DecodeTargetInformation_PairAfterAFailure_DoesNotLeakPartialPairs`, `DecodeTargetInformation_SeededRandomBytes_NeverThrowAndFailOnlyWithAListFailure` (seed 15031), `TryEncode_UnpairedSurrogateInUser_WritesTheUtf8ReplacementCharacter`, `TryEncode_NulInsideUser_KeepsItAndTheBytesAfterIt`.
- **Invalid partitions:** `Decode_MessageTypeOtherThanChallenge_RefusesAsWrongSignatureOrType` (0, 1, 3, high byte set, max), `Decode_ZeroLengthTargetInfoAtAnOutOfRangeOffset_DecodesWithoutTargetInformation`, `Decode_OutOfRangeTargetInfoWithTargetInfoFlagClear_IgnoresIt`, `Decode_AnyUnicodeAndOemCombination_KeepsTheFlagsAndTargetNameBytesAsSent` (neither, each, both, all bits), `TryEncode_NonAsciiUserWithUnicodeFlag_WidensEachUtf8ByteAsCurlDoes`, `TryEncode_NonAsciiUserWithUnicodeFlagClear_WritesItsUtf8Bytes`, `ComputeNtOwfV2_NonAsciiUser_UppercasesAsciiLettersOnly`, `Answer_UserNameMadeOfSeparators_SplitsAtTheFirstBackslashElseSlash`, `Answer_NullUserName_ThrowsArgumentNullException`, `Answer_NullPassword_ThrowsArgumentNullException`, `ComputeV1_ServerChallengeOfWrongLength_ThrowsArgumentException`, `ComputeV2_ChallengeOfWrongLength_ThrowsArgumentException`, `EncryptSessionKey_KeyOfWrongLength_ThrowsArgumentException`.
- **State and concurrency:** `Decode_InputChangedAfterDecoding_LeavesTheDecodedMessageUnchanged`, `Decode_SameMessageTwiceAfterChangingTheFirstResult_ReturnsTheOriginalBytes`, `Decode_ManyConcurrentCallers_AllReadTheSameMessage`, `TryEncode_CalledTwice_ReturnsEqualBytesInSeparateArrays`, `ComputeV2_CallerChallengeArrays_AreLeftUnchanged`, `Answer_TwoClientChallenges_GiveDifferentNtlmV2Responses`, `Answer_SubSecondClockDifferences_GiveTheSameNtlmV2Response`, `Answer_ManyConcurrentCallersOnOneAnswerer_AnswerAsOneAfterAnother`. Cancellation and partial delivery do not apply: the library takes whole spans and has no async or stream API.

Defects filed (no test committed for either; each lands with its fix):
- BL-1654 (Normal): `Answer` with a `TimeProvider` before 1601 lets `ArgumentOutOfRangeException` from `DateTimeOffset.ToFileTime` escape, an exception no doc comment names. Not High: a real clock never reads before 1601.
- BL-1655 (Low): `Answer` on a hand-built challenge whose `ServerChallenge` is not 8 bytes throws `ArgumentException` its doc does not name; `Decode` never produces one.

Test count: 66 before, 159 after (`dotnet test Curl.Ntlm.UnitTests --filter "TestCategory!=Integration"`); `dotnet build Curl.Ntlm.UnitTests -warnaserror` clean, `dotnet format --verify-no-changes` clean.

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Curl.Ntlm.UnitTests attacks NTLM Type 2 parsing, AV pairs and Type 3 output in all four families (66 -> 159 tests); defects filed as BL-1654, BL-1655
