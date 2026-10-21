---
id: BL-1502
title: Attack Curl.Networking.UnitLibrary with adversarial black-box tests in Curl.Networking.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1450, BL-1455, BL-1468]
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1502 — Attack Curl.Networking.UnitLibrary with adversarial black-box tests in Curl.Networking.UnitTests

## Goal

`Curl.Networking.UnitTests` gains adversarial black-box tests that attack `Curl.Networking.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1450, BL-1455, BL-1468.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Networking.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: DNS answer decoding (compression-pointer loops, pointers past the end, truncated records, zero answers), `--resolve` and `--connect-to` syntax with IPv6, wildcards and missing parts, address-family racing at its timing boundaries through `TimeProvider`, connection-cache reuse under interleaved and repeated transfers, and certificate and CRL files that are empty, truncated, PEM with junk around it or DER with bad lengths.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Networking.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Networking.UnitTests/` and this task file.

## Notes

All tests are in `Curl.Networking.UnitTests/NetworkingAdversarialTests.cs`. The public surface attacked is `DnsAnswerDecoder`, `ServiceBindingRecordDecoder`, `ResolveOverrides` and `ConnectToMappings`; `DnsCache` (members internal), `CertificateRevocationListFile`, `PinnedPublicKey` and `DohResponseReader` (internal types) are outside the black-box surface, so CRL and certificate files were not attacked here. Address-family racing and connection-cache reuse go through `TcpConnector` and `PoolingConnector`, whose timing and reuse paths the existing `TcpConnectorTests.HappyEyeballs` and `PoolingConnector*Tests` suites already drive with the fake `TimeProvider`; this task added no tests for those.

- **Boundaries:** `DnsAnswerDecode_MessageShorterThanTheHeader_IsTooSmall` (0, 1, 11), `DnsAnswerDecode_BareHeaderWithNoRecords_IsNoContent`, `DnsAnswerDecode_ARecordsAroundTheTwentyFourAddressLimit_KeepsAtMostTwentyFour`, `DnsAnswerDecode_CnameRecordsAroundTheFourNameLimit_KeepsAtMostFour`, `DnsAnswerDecode_CnameTargetAroundTheHundredAndTwentyEightLabelLimit_StopsAtTheLimit`, `DnsAnswerDecode_AddressDataOneByteOffItsSize_IsRdataLength`, `DnsAnswerDecode_TtlAtItsExtremes_ReportsTheSmallerOfItAndIntMaxValue`, `ServiceBindingDecode_DataShorterThanPriorityAndRoot_IsTruncated`, `ServiceBindingDecode_PriorityAndRootOnly_DecodesToTheRootTarget`, `ServiceBindingDecode_TargetLabelAroundSixtyThreeBytes_RefusesOnlyTheLongerOne`, `ServiceBindingDecode_KnownParameterOfTheWrongShape_IsBadParameterValue`, `ResolveParse_PortAtTheEdgesOfItsRange_Parses`, `ConnectToMap_DestinationPortAtTheTopOfItsRange_Maps`.
- **Malformed input:** `DnsAnswerDecode_CnamePointingAtItself_IsLabelLoopWithoutHanging`, `DnsAnswerDecode_TwoCnamesPointingAtEachOther_IsLabelLoopWithoutHanging`, `DnsAnswerDecode_CnameTargetRunningPastTheEnd_FailsWithoutThrowing`, `DnsAnswerDecode_QuestionLabelWithReservedTopBits_IsBadLabel`, `DnsAnswerDecode_ValidAnswerCutAtEveryOffset_FailsWithoutThrowing`, `DnsAnswerDecode_ByteAfterTheLastRecord_IsMalformed`, `DnsAnswerDecode_AnswerCountFarAboveTheRecordsPresent_IsOutOfRange`, `DnsAnswerDecode_ValidAnswerWithAnyOneBitFlipped_NeverThrows`, `DnsAnswerDecode_SeededRandomMessages_NeverThrowAndNeverExceedTheLimits` (seed 1502), `ServiceBindingDecode_CompressedOrReservedTargetLabel_IsBadTargetName`, `ServiceBindingDecode_ParameterLengthPastTheEnd_IsParameterOverrun`, `ServiceBindingDecode_ValidRecordCutAtEveryOffset_FailsOrDecodesWithoutThrowing`, `ServiceBindingDecode_SeededRandomData_NeverThrowsAndRecordMatchesFailure` (seed 9460).
- **Invalid partitions:** `DnsAnswerDecode_NonZeroMessageId_IsBadId`, `DnsAnswerDecode_NonZeroResponseCode_IsBadRcode`, `DnsAnswerDecode_AnswerOfTheWrongTypeOrClass_IsRefused`, `ResolveParse_EntryInAnInvalidPartition_FailsWithCurlsExit49Text`, `ConnectToMap_DestinationInAnInvalidPartition_FailsWithCurlsExit49Text`, `ConnectToMap_MappingWithoutAWholeSource_LeavesTheConnectionUnmapped`. The exit 49 texts were measured with real curl 8.21.0 (Windows, Schannel) on 2026-10-07: `--resolve h:65536:1.2.3.4`, `h::1.2.3.4`, `h:80:`, `h:80:,,`, `h:80:1.2.3.4.5`, `h:80:fe80::1%eth0`, `h:80:1.2.3.4,bad`, and `--connect-to h:1:x:65536` and `h:1:[::1:9`. These are parse errors, so no network was used.
- **State and concurrency:** `DnsAnswerDecode_BodyChangedAfterTheDecode_LeavesTheAnswerAsDecoded`, `DnsAnswerDecode_SameMessageOnManyThreadsAtOnce_AgreesWithOneDecode`, `ResolveParse_AddRemoveAndReAddOfOneHost_EndsWithTheLastAddition`, `ResolveFind_FromManyThreadsAtOnce_AgreesWithSequentialAnswers`, `ConnectToMap_FromManyThreadsAtOnce_AgreesWithSequentialAnswers`.
- **Defects found:** none. Every attack passed against the library as it is, so no follow-up task was filed.
- **Test count** for `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"`: 3063 before (3035 passed, 28 skipped) and 3149 after (3121 passed, 28 skipped). No input is over 1 MiB, so no test needed `Integration`.

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Curl.Networking.UnitTests attacks the DNS answer and HTTPS record decoders, --resolve and --connect-to parsing with 86 adversarial tests; no defects found
