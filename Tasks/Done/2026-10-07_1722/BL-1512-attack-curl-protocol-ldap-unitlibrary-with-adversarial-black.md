---
id: BL-1512
title: Attack Curl.Protocol.Ldap.UnitLibrary with adversarial black-box tests in Curl.Protocol.Ldap.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1478]
touches: [Curl.Protocol.Ldap.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1512 — Attack Curl.Protocol.Ldap.UnitLibrary with adversarial black-box tests in Curl.Protocol.Ldap.UnitTests

## Goal

`Curl.Protocol.Ldap.UnitTests` gains adversarial black-box tests that attack `Curl.Protocol.Ldap.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1478.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Protocol.Ldap.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: LDAP URLs (DN escaping, unbalanced and escaped filters, scope and attribute parts that are missing, empty or unknown) and BER responses with long-form, indefinite and overflowing lengths, unexpected tags and truncated messages.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Protocol.Ldap.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Protocol.Ldap.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Ldap.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Protocol.Ldap.UnitTests/` and this task file.

## Notes

All new tests are in `Curl.Protocol.Ldap.UnitTests/LdapAdversarialTests.cs`. They drive the library's types through the members the existing tests already use (no new `InternalsVisibleTo`, no reflection) and use `ScriptedConnection` for the wire. No test reaches the network and no input is over 1 MiB, so none is `Integration`. The oracle is each type's own contract (a named refusal, never an exception), because a parser shows curl nothing directly. Fuzz tests use a fixed seed (1512) and log it through `TestDiagnostics`.

- **Boundaries**: `MeasureFrame_LengthAtAndPastArrayMaxLengthAndNonMinimalLongForm_IsTheLengthOrMalformed` (a frame of exactly `Array.MaxLength` and one byte past it, a 2^31 length, the long form used for a short value, 0x7f length octets), `SearchDecode_ResultCodeAtAndPastIntMaxValueOrNegative_IsDoneOrLost`, `BindDecode_ResultCodeAtAndPastIntMaxValueOrNegative_IsAnsweredOrMalformed`, `SearchDecode_MessageIdPastIntMaxValue_IsLost`, `ReadMessageAsync_FrameLargerThanTheFirstBuffer_GrowsAndReturnsIt`, `Encode_FilterNestedTwoHundredDeep_EncodesOneConstructedPerLevel`, `TryRead_EscapeCutOffOrNotHex_ReadsOnlyAWholeEscape`.
- **Malformed input**: `SearchDecode_EntryWithIndefiniteLengthAttributeList_IsTheEntry`, `SearchDecode_TrailingBytesAfterTheMessage_IsLost`, `SearchDecode_EveryTruncation_NeverThrows`, `BindDecode_EveryTruncation_IsMalformed`, `SearchDecode_SeededRandomByteMutations_NeverThrows`, `BindDecode_SeededRandomByteMutations_NeverThrows`, `MeasureFrame_SeededRandomPrefixes_NeverThrowsAndNeverClaimsMoreThanArrayMaxLength`, `Encode_SeededRandomFilters_NeverThrowInEitherDialect`, `Read_SeededRandomQueries_NeverThrowInEitherBuild`.
- **Invalid partitions**: `Encode_UnbalancedOrEmptyFilter_IsRefusedInBothDialects` (unclosed, unopened, an empty item, an empty `!`, a trailing backslash, no attribute, an extra `)`, no operator), `Encode_ParenthesisOrBlankInsideTheAttribute_IsRefusedByOpenLdap`, `Read_InvalidQueryPartitions_IsASearchOrARefusalWithExit3InBothBuilds` (an unknown scope, a cut-off or non-hex escape in the filter, empty and extra extensions, a NUL in the attributes and the DN, a 256-character attribute). WinLDAP accepts `cn=a)`, `((cn=a)` and `(c n=a)` because its attribute rules are laxer. Those cases are pinned for OpenLDAP only, not called a defect: there is no Windows curl measurement saying otherwise.
- **State and concurrency**: `ReadMessageAsync_LongFormFrameSplitAtEveryOffset_ReturnsTheWholeMessage`, `ReadMessageAsync_ConnectionClosesInsideAClaimedHugeFrame_IsClosedWithoutWaitingForTheClaim`, `ReadMessageAsync_CalledAgainAfterClosed_StaysClosed`, `ReadMessageAsync_CalledAgainAfterMalformed_StaysMalformed`, `ReadMessageAsync_ManyMessagesInOneRead_ReturnsEachInOrder`, `Encode_ReusedAfterARefusal_EncodesTheNextFilterAsAFreshEncoderDoes`, `SearchDecode_SameMessageOnManyTasksAtOnce_GivesTheSameReplyAsOneCall`. Time and cancellation do not apply: the reader takes no `TimeProvider` and passes its token straight to `IConnection`. The filter encoder is stateful and does not claim to be thread-safe, so it is not run concurrently.
- **Defects filed**: BL-1661 (High). `LdapFilterEncoder` recurses once per nesting level with no limit, so a deeply nested filter in an URL can overflow the stack and kill the process. This was found by reading the code and was not reproduced, because a stack overflow would take the test host down with it. Every other attack passed.
- Test count: 538 before, 601 after (`dotnet test Curl.Protocol.Ldap.UnitTests --filter "TestCategory!=Integration"`). `dotnet build Curl.Protocol.Ldap.UnitTests -warnaserror` is clean and `dotnet format --verify-no-changes` passes on the new file.

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Curl.Protocol.Ldap.UnitTests attacks the LDAP frame reader, BER decoders, filter encoder and URL readers with 63 adversarial tests; BL-1661 filed
