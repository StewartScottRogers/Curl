---
id: BL-1494
title: Attack Curl.Conformance.UnitLibrary with adversarial black-box tests in Curl.Conformance.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1440, BL-1445, BL-1460]
touches: [Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1494 — Attack Curl.Conformance.UnitLibrary with adversarial black-box tests in Curl.Conformance.UnitTests

## Goal

`Curl.Conformance.UnitTests` gains adversarial black-box tests that attack `Curl.Conformance.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1440, BL-1445, BL-1460.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Conformance.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: the upstream test-case tooling: `UpstreamTestCaseParser` on malformed, truncated and deeply nested test files, include loops and Perl substitutions that never terminate, `UpstreamCommandLineSplitter` on unbalanced quotes and trailing backslashes, and the sws request framing and reply selection on truncated, oversized and pipelined requests.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Conformance.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Conformance.UnitTests -warnaserror` is clean and `dotnet test Curl.Conformance.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Conformance.UnitTests/` and this task file.

## Notes

- Public surface attacked: `UpstreamTestCaseParser`, `UpstreamTestFileExpander` and `SwsHttpServerConnector`. `UpstreamCommandLineSplitter`, `SwsHttpRequestFraming` and `SwsHttpReplySelector` are `internal`, so they are attacked only through the public connector and expander (black box: no `InternalsVisibleTo` use). Nothing here is visible on curl's command line, so the oracle is the library's documented contract and upstream's `getpart.pm`/`prepro`/`sws.c` behaviour as the doc comments describe it; no `Record-CurlExchange.ps1` measurement applied.
- Layout choice: the tests are the second half of `partial` test classes, in `UpstreamTestCaseParserTests.Adversarial.cs`, `UpstreamTestFileExpanderTests.Adversarial.cs` and `SwsHttpServerConnectorTests.Adversarial.cs`, keeping `.claude/rules/testing.md`'s one test class per production class while keeping the attacks easy to find. The three existing classes gained `partial`; the project's CLAUDE.md says so.
- **Boundaries:** `Parse_EmptyFile_ParsesAsACaseWithNoSections`, `Parse_LastLineWithoutLineFeed_StillClosesTheFile`, `Parse_TenThousandNestedPartTagsInsideAPart_KeepsThemAsBody`, `Expand_RepeatCountOfZero_LeavesNothing`, `Expand_InstructionThatDoesNotMatch_IsLeftAsWritten` (count past `int.MaxValue`), `Expand_VariableWithAnEmptyName_IsNeverMatched`, `Expand_TenThousandNestedIfBlocks_DropsTheirLinesAndKeepsTheRest`, `HeaderLineOfHalfAMebibyteWithNoEnd_IsRecordedAndNeverAnswered` (512 KiB, under the 1 MiB Integration line).
- **Malformed input:** `Parse_TruncatedAtEveryOffset_ReturnsAResultWithoutThrowing`, `Parse_TruncatedInsideAPart_NamesThePartThatIsNeverClosed`, `Parse_AttributeWithUnterminatedQuote_ReadsNoAttribute`, `Parse_DuplicatedAttribute_KeepsTheLastValueWithoutThrowing`, `Parse_BodyWithNulAndHighBytes_KeepsEveryByte`, `Parse_SeededRandomTagLines_NeverThrows` (seed 1494), `Expand_InstructionThatDoesNotMatch_IsLeftAsWritten`, `Expand_HexWithAMalformedPair_KeepsItsCharacters`, `Request_CutShortThenDisposed_IsRecordedAndNeverAnswered`, `MalformedRequest_IsRecordedWithoutThrowing` (NUL/0xFF line, empty line, method only, bare LFs, negative Content-Length, bad and overflowing chunk sizes), `CaseWithNoReplySection_AnswersWithoutThrowing`.
- **Invalid partitions:** `Parse_ClosingTagOnTheFirstLine_FailsNamingIt`, `Parse_SectionClosedByTheWrongName_FailsNamingTheClosingTag`, `Parse_NestedPartTagNeverClosed_FailsAtTheEnclosingSectionsClose`, `Parse_CarriageReturnOnlyLineEndings_ReadsOneLineThatIsNeverClosed`, `Expand_StrayConditionDirective_StopsAndNamesIt`, `Expand_IfNeverClosed_DropsToTheEndWithoutAnError`, `Expand_HexThatDecodesToAnotherHexInstruction_ExpandsItUntilNoneIsLeft`, `Expand_VariableWhoseValueNamesItself_IsSubstitutedOnce`, `Expand_IncludedFileThatIncludesItself_IsReadOnceAndNotSearchedAgain`, `Expand_IncludedTextThatIncludesItselfRaw_IsReadOncePerPass` (include loops terminate).
- **State and concurrency:** `Get_WrittenOneBytePerWrite_IsAnsweredOnlyAfterTheLastByte`, `TwoRequestsInOneWrite_RecordsBothAndAnswersTheFirstFirst` (pipelined), `Abandon_Twice_ThenEveryExchangeThrowsIOException`, `Dispose_Twice_RecordsOneDisconnect`, `ManyConnectionsWithInterleavedHalfRequests_EachIsAnsweredAndEveryByteIsRecorded`, `Parse_SameFileOnManyTasksAtOnce_GivesTheSameCaseEachTime`, `Expand_SameFileOnManyTasksAtOnce_GivesTheSameExpansionEachTime`. Time: the connector's timing commands are already driven on `ManualTimeProvider` by the existing tests; no new time attack found a gap.
- **Defects filed:** BL-1645 (High) — connections written on many threads at once lose recorded bytes: `SwsServerRecording` appends to an unlocked `List<byte>`; a 32-thread stress run failed on its first round. The interleaved test above runs on one thread and names BL-1645. BL-1646 (High) — `%repeat[2000000000 x ab]%` asks for a string past the CLR limit and throws `OutOfMemoryException`; found by reading, not run, since it would take gigabytes on a machine nine lanes share.
- Test count, `dotnet test Curl.Conformance.UnitTests --filter "TestCategory!=Integration"`: before 2502 (1048 passed, 1454 skipped), after 2551 (1097 passed, 1454 skipped), 0 failed. The skips are the conformance ratchet's unlisted upstream cases, unchanged.

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Curl.Conformance.UnitTests attacks the parser, expander and sws emulation in all four families with 49 new tests; defects filed as BL-1645 and BL-1646
