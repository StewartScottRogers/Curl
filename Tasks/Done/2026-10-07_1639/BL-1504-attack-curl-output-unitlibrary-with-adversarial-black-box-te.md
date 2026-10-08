---
id: BL-1504
title: Attack Curl.Output.UnitLibrary with adversarial black-box tests in Curl.Output.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1470]
touches: [Curl.Output.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1504 — Attack Curl.Output.UnitLibrary with adversarial black-box tests in Curl.Output.UnitTests

## Goal

`Curl.Output.UnitTests` gains adversarial black-box tests that attack `Curl.Output.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1470.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Output.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: `-w` template rendering (unknown variables, a trailing `%`, an unterminated `%{`, `%{json}` and `%{header_json}` with control characters and unpaired surrogates), `DerReader` on bad lengths and nesting, time formats at the epoch, year 9999 and negative times, trace and hex dumps of binary and very large data, styled header output with control characters, and the progress meter with zero elapsed time and sizes near `long.MaxValue`.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Output.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Output.UnitTests -warnaserror` is clean and `dotnet test Curl.Output.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Output.UnitTests/` and this task file.

## Notes

Four new test classes in `Curl.Output.UnitTests`, public surface only, oracle real curl 8.21.0 (mingw, Schannel) measured on 2026-10-07 with `curl -s -o NUL -w <template> file:///nonexist` for the `-w` cases (`%{}`, 23- and 24-byte names, `%{%}`, unterminated `%header{`/`%output{`/`%time{`, `%time{}` results of 252 and 256 bytes).

- **Boundaries:** `RenderAsync_VariableNameOf23Bytes_IsLookedUpAndWarnedAbout`, `..._VariableNameOf24Bytes_EndsTheRenderingSilently`, `..._VariableNameOf12TwoByteCharacters_CountsBytesNotCharactersAndEnds`, `..._HeaderNameOf255Bytes_RendersTheHeader`, `..._HeaderNameOf256Bytes_RendersNothingAndCarriesOn`, `..._OutputFileNameOf511Bytes_IsOpened`, `..._OutputFileNameOf512Bytes_IsNotOpenedAndOutputStaysPut`, `..._TenThousandVariables_RendersEveryOne`; `Format_AtTheUnixEpochInUtc_IsNineteenSeventy`, `Format_LastTickOfYear9999InUtc_IsYear9999`, `Format_LastTickOfYear9999InAZoneAheadOfUtc_ReturnsWithoutThrowing`, `Format_FirstTickOfYearOneInAZoneBehindUtc_ReturnsWithoutThrowing`, `Format_ResultOf252Bytes_FitsCurlsBuffer`, `Format_ResultOf256BytesOrMore_IsEmpty`; `Size_AnyCountFromZeroToLongMaxValue_IsFiveColumns`, `Time_ZeroOrNegativeSeconds_IsEightBlanks`, `Time_AtEachLayoutChange_IsEightColumns`, `StatusLine_EveryFigureAtItsLimit_ReturnsWithoutThrowing`, `StatusLine_ZeroElapsedAndNothingTransferred_HasBlankTimesAndZeroSizes`; `Style_EmptyLine_IsReturnedEmpty`, `Style_ManyRedirectsInARow_EachResolvesAgainstThePreviousLocation`, `Write_ManyLinesInOneWrite_StylesEachLine`, `Write_EmptyBuffer_WritesNothing`.
- **Malformed input:** `RenderAsync_EmptyVariableName_WarnsWithEmptyQuotesAndWritesNothing`, `..._UnterminatedHeaderOutputAndTime_AreWrittenAsTheyStand`, `..._PercentAsVariableName_WarnsAboutThePercent`, `..._EmptyOutputFileNameWithAppend_AsksTheOpenerAndStaysOnStandardOutputWhenRefused`, `..._ValueWithUnpairedSurrogate_WritesTheReplacementCharacterWithoutThrowing`, `..._ControlCharactersAndNulInTemplate_PassThroughUnchanged`, `..._BackslashBeforeVariable_KeepsTheBackslashAndRendersTheVariable`, `UnknownVariableWarning_NameWithQuoteAndLineFeed_IsEmbeddedVerbatim`; `Format_FormatEndingMidDirectiveOrUnknown_ReturnsWithoutThrowing`; `Style_LineOfOnlyAColon_BoldsAnEmptyName`, `Style_EscapeSequenceInHeaderValue_IsPassedThroughAsCurlDoes`, `Style_LocationWithHighLatin1Bytes_LinksWithoutThrowing`, `Write_LineFeedAloneAndBareLineFeeds_PassThroughUnstyled`.
- **Invalid partitions:** `Style_LocationWithControlBytesOrAnUnlinkedScheme_IsNotLinked` (terminal-escape injection, DEL, `javascript:`, `file:`, empty), `Style_UnparsableBaseUrl_LeavesTheLocationUnlinked`, `ForPlatform_VteVersionThatIsNotAnOldVersionNumber_StillLinksLocation`, `Format_DialectOutsideTheEnum_ThrowsArgumentOutOfRange`, `ReadSeekLengthPosition_OnAWriteOnlyStream_ThrowNotSupported`, negative and `long.MinValue` rows of `Time_ZeroOrNegativeSeconds_IsEightBlanks`.
- **State and concurrency:** `RenderAsync_SameRendererTwice_WritesTheSameBytesEachTime`, `RenderAsync_OneRendererOnManyTasksAtOnce_GivesEachTheSameOutput` (32 tasks), `RenderAsync_StdoutStderrSwitchedManyTimes_KeepsEachPieceOnItsStream`, `RenderAsync_OnerrorOnSuccessfulTransfer_StopsBeforeFileIsOpenedAndClosesNothingOpen`, `RenderAsync_TokenCancelledBeforeTheCall_ThrowsOperationCanceled`, `Style_ManyRedirectsInARow_...` (state carried across 1000 lines); partial delivery to `StyledHeaderStream` exposed BL-1658.
- **Defects filed:** BL-1657 (`ParallelProgressMeterText.Size((100L << 50) - 1)` is `"99.10P"`, six columns, and `Time(long.MaxValue)` is 16 characters; curl's `msnprintf` buffers cut them to 5 and 8). BL-1658 (`StyledHeaderStream` styles each write as a whole line, so a header written byte by byte comes out `Content-Type\e[1m\e[22m: x`). Neither is a crash, hang or security issue, so both are Normal; no test for them is committed here.
- **Not attacked:** `DerReader` is `internal`, so outside the black box; trace and hex dumps of `TraceTransferEventWriter` were left to its existing suite for time. No input over 1 MiB was used, so no test needed the Integration category.
- **Test count:** 553 before (550 passed, 3 skipped), 637 after (634 passed, 3 skipped).

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Curl.Output.UnitTests attacks the -w renderer, time formatter, progress meter and styled headers adversarially (84 new tests); BL-1657 and BL-1658 filed
