---
id: BL-1523
title: Attack Curl.Tls.UnitLibrary with adversarial black-box tests in Curl.Tls.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1489]
touches: [Curl.Tls.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1523 — Attack Curl.Tls.UnitLibrary with adversarial black-box tests in Curl.Tls.UnitTests

## Goal

`Curl.Tls.UnitTests` gains adversarial black-box tests that attack `Curl.Tls.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1489.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Tls.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: TLS records at 2^14, 2^14+1 and 2^14+256 bytes, handshake messages fragmented across records and records holding several, duplicate and unknown extensions, unknown versions and alerts, certificate chains with bad DER lengths, SNI with IDN, IP literals and trailing dots, and ALPN lists that are empty or overlong.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Tls.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Tls.UnitTests -warnaserror` is clean and `dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Tls.UnitTests/` and this task file.

## Notes

All new tests are in `Curl.Tls.UnitTests/TlsAdversarialTests.cs` (27 test cases, public surface only, no network, no platform-specific text, nothing over 1 MiB so none is Integration). Fast test count before 1283, after 1310 (`dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration"`).

- **Boundaries:** `HandshakeReader_ThreeHeaderBytes_NeedsMoreBytes`, `HandshakeReader_LargestUInt24LengthWithNoBody_NeedsMoreBytesWithoutThrowing`, `HandshakeReader_ZeroLengthBody_CompletesWithAnEmptyBody`, `Alpn_ProtocolNameOf255Bytes_RoundTrips`, `SupportedVersions_SelectedNotExactlyTwoBytes_IsDecodeError` (0, 1 and 3 bytes). TLS records at 2^14 and past it are not repeated: the record layers are internal, and the public connections already pin them (`Tls12ClientStreamTests.RecordLongerThanAProtectedRecordMayBeIsARecordOverflow`, `Tls13ClientConnectionTests.PlaintextRecordOverTheLimitIsARecordOverflow`, `Tls12RecordProtectionTests`).
- **Malformed input:** `HandshakeReader_TwoMessagesInOneBuffer_ConsumesOnlyTheFirst`, `HandshakeReader_MessageFragmentedByteByByte_CompletesOnlyOnTheLastByte`, `Alpn_ProtocolLengthPastTheListEnd_IsDecodeError`, `Alpn_ListLengthLargerThanTheData_IsDecodeError`, `ServerName_TwoEntriesInTheList_IsDecodeError`, `ServerName_EmptyData_IsDecodeError`, `Certificate_DerLengthLargerThanTheList_IsDecodeError`, `Certificate_EntryWithNoExtensionBlock_IsDecodeError`, `Certificate_DuplicateExtensionOnOneEntry_IsIllegalParameter`, `EncryptedExtensions_DuplicateUnknownExtension_IsIllegalParameter`.
- **Invalid partitions:** `HandshakeReader_UnknownMessageType_FailsWithUnexpectedMessage` (0x03, 0xFF), `ServerName_AcknowledgementWithData_IsDecodeError`, `ServerName_HostNameShapes_RoundTripByteForByte` (trailing dot, IP literal, IDN as its A-label), `EncryptedExtensions_UnknownExtensionType_IsKeptNotRefused`, `DecodeResult_ValueOfAFailure_ThrowsInvalidOperationNamingTheAlert`.
- **State and concurrency:** `Decoders_SameBufferDecodedInParallel_AgreeAndLeaveTheBufferUnchanged` (256 parallel ALPN decodes of one buffer), `HandshakeReader_SameBufferReadRepeatedly_AnswersTheSameEveryTime`. The decoders are stateless; the stateful connections are already driven through interleaved sends and receives by the existing client stream tests.
- **Defects filed** (no failing test committed): BL-1712 - ALPN `Decode` accepts an empty list and an empty name (RFC 7301), `Encode` accepts an empty list and throws an undocumented `ArgumentException` on a 256-byte name. BL-1713 - `CertificateMessage.Decode` accepts zero-length `cert_data` (RFC 8446) and `ServerNameExtension.DecodeHostName` an empty host name (RFC 6066). Both Normal: wrong acceptance, no crash, hang or unbounded memory.
- Oracle: the RFCs, since none of these decoder answers is visible on curl's command line, so `Record-CurlExchange.ps1` was not needed.

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. 27 adversarial black-box tests attack Curl.Tls decoders; BL-1712 and BL-1713 filed
