---
id: BL-1501
title: Attack Curl.Kerberos.UnitLibrary with adversarial black-box tests in Curl.Kerberos.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1467]
touches: [Curl.Kerberos.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1501 — Attack Curl.Kerberos.UnitLibrary with adversarial black-box tests in Curl.Kerberos.UnitTests

## Goal

`Curl.Kerberos.UnitTests` gains adversarial black-box tests that attack `Curl.Kerberos.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1467.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Kerberos.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: ASN.1 DER and Kerberos messages: long-form, indefinite and overflowing lengths; unexpected tags; truncated tickets and authenticators; unsupported encryption types; clock skew exactly at, below and above the allowed window through `TimeProvider`; replayed authenticators.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Kerberos.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Kerberos.UnitTests -warnaserror` is clean and `dotnet test Curl.Kerberos.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Kerberos.UnitTests/` and this task file.

## Notes

All new tests are in `Curl.Kerberos.UnitTests/KerberosAdversarialTests.cs`; they use only the public types plus the project's existing fakes (`FakeGssAcceptor`, `FixedTimeProvider`, `FixedKerberosRandomSource`, `BigEndianBytes`, the recorded messages and files). No network, no paths, no input over 1 MiB, so none is Integration. Mutations use a seeded `System.Random`; a failure lists seed, iteration and the mutated bytes. Oracle: the RFCs and each member's doc comment (no command-line behaviour is involved, so no `Record-CurlExchange.ps1` measurement).

Test count: 707 before, 776 after (`dotnet test Curl.Kerberos.UnitTests --filter "TestCategory!=Integration"`).

- **Boundaries** - `KdcReplyDecode_OuterLengthInLongFormWithLeadingZeros_DecodesAsTheDerForm`, `KdcReplyDecode_OuterLengthPaddedPastFourBytesWithZeros_DecodesAsTheDerForm` (5 and 9 length bytes: BER allows it), `KdcReplyDecode_OuterIndefiniteLength_DecodesAsTheDerForm`, `KdcReplyDecode_OuterLengthOverflowingOrWrong_FailsAsMalformed` (2^32-1, int.MaxValue, 0xFF, one byte long and short), `CredentialCacheRead_ComponentCountOfUInt32Max_FailsAsTruncated`, `CredentialCacheRead_HeaderLengthWrong_FailsAsTruncated`, `CredentialCacheRead_DeltaTimeShorterThanEightBytes_FailsAsTruncated`, `CredentialCacheRead_DeltaTimeAtInt32Extremes_ReadsTheOffset`, `KeytabRead_EntrySizeRunningPastTheEnd_FailsAsTruncated` (int.MinValue hole, int.MaxValue, one past), `KeytabRead_FewerThanFourBytesAfterTheVersion_IsEmpty`, `KeytabRead_HoleOfExactlyTheRemainingBytes_IsEmpty`, `KeytabRead_ShorterThanTheVersion_FailsAsTruncated`, `EveryEncryptionType_KeyOneByteShortOrLong_ThrowsArgumentException`, `EveryEncryptionType_CiphertextOfEveryLengthUpToThreeBlocksPastTheMinimum_RefusesOnlyWithACryptographyException`, `Aes256StringToKey_IterationCountParametersOutOfRange_FailsAsBadStringToKeyParameters`. Clock skew at, below and above the window: the library is the client only and checks no skew window itself (the KDC does, answering KRB_AP_ERR_SKEW, already pinned by `KerberosKdcExceptionTests`); its one time check is the AP-REP's exact echo of the authenticator time, attacked by `GssContext_ApReplyEchoingAnotherAuthenticatorTime_FailsMutualAuthentication` (-1 us, +1 s, microseconds zeroed).
- **Malformed input** - `KdcReplyDecode_IndefiniteLengthWithoutEndOfContents_FailsAsMalformed`, `EveryMessageDecoder_MutatedRecordedMessages_RefusesOnlyWithAMessageException` (14 public decoders x 7 recorded messages, 2 seeds x 400 mutations), `KdcReplyDecode_TruncatedAtEveryOffset_FailsAsMalformed`, `CredentialCacheRead_TruncatedAtEveryOffset_FailsAsTruncatedOrEndsAtACredential`, `CredentialCacheRead_MutatedRecordedCache_RefusesOnlyWithAFileException`, `KeytabRead_EntryTooShortForItsFields_FailsAsTruncated`, `KeytabRead_MutatedRecordedKeytab_RefusesOnlyWithAFileException`, `EveryEncryptionType_EveryBitOfAShortCiphertextFlipped_FailsTheIntegrityCheck`, `GssContext_ApReplyTruncatedAtEveryOffset_RefusesOnlyWithAGssException`, `GssContext_MutatedAcceptorTokens_RefuseOnlyWithAGssException` (RFC 4121 AES-SHA1, AES-SHA2 and RFC 4757 RC4 Wrap and MIC tokens).
- **Invalid partitions** - `KdcReplyDecode_UnexpectedOuterTag_FailsWithAMessageException` (universal, context, private, primitive, high-tag-number, end-of-contents), `KeytabRead_VersionOtherThanTwo_FailsAsUnknownVersion`, `EncryptionCreate_UnsupportedEncryptionType_FailsAsUnsupportedEncryptionType` (int.MinValue, -128, 0, des-cbc-crc, des-cbc-md5, 24, 27, int.MaxValue), `GssContext_ServiceTicketOfAnUnsupportedEncryptionType_FailsAsUnsupportedEncryptionType`.
- **State and concurrency** - `EveryEncryptionType_SharedAcrossThreads_RoundTripsEveryMessage` (64 tasks per type on one instance), `GssContext_PerMessageCallsBeforeTheContextIsEstablished_ThrowInvalidOperation`, `GssContext_NextTokenAfterTheContextIsEstablished_ThrowsInvalidOperation`; replayed, skipped and reflected tokens were already pinned by `KerberosGssMessageTokenTests` and `Des3CbcSha1GssMessageProtectionTests`, so not repeated.

Defects found:
- **BL-1647** (High): a des3-cbc-sha1 base key that .NET's `TripleDES` calls weak (e.g. all zeros) makes `Encrypt`/`Decrypt` throw an undocumented `CryptographicException` from `TripleDES.SetKey`. No test for it is committed here; it lands with the fix.

Not defects (my first expectations were wrong): BER lengths padded with zero bytes past four length octets decode (BER permits it), and des3-cbc-sha1's `Decrypt` returns its zero padding by design (ADR-0237).

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Curl.Kerberos.UnitTests attacks the library's decoders, file readers, encryption types and GSS context with 69 adversarial tests (707 -> 776); the des3 weak-key crash is filed as BL-1647
