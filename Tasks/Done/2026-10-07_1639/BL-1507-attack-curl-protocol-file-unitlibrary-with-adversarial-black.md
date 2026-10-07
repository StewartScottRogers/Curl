---
id: BL-1507
title: Attack Curl.Protocol.File.UnitLibrary with adversarial black-box tests in Curl.Protocol.File.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1451, BL-1473]
touches: [Curl.Protocol.File.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1507 — Attack Curl.Protocol.File.UnitLibrary with adversarial black-box tests in Curl.Protocol.File.UnitTests

## Goal

`Curl.Protocol.File.UnitTests` gains adversarial black-box tests that attack `Curl.Protocol.File.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1451, BL-1473.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Protocol.File.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: `file://` URLs: empty host, `localhost`, another host, `%2F` and `..` segments, Unicode and very long names, a directory, a missing file, ranges (`-r`) at, beyond and before the file's size, and uploads to an existing and a read-only file; drive-less paths only, per the platform rule.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Protocol.File.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Protocol.File.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.File.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Protocol.File.UnitTests/` and this task file.

## Notes

Two new classes in `Curl.Protocol.File.UnitTests`: `FileUrlPathAdversarialTests` (the URL path split) and `FileProtocolHandlerAdversarialTests` (the transfer). Public surface only (`FileUrlPath.TryParse(CurlUrl, out)`, `FileProtocolHandler`), the existing `FakeFileSystem` plus one private thread-safe read-only `IFileSystem` for the concurrency test; MSTest and the BCL, no I/O. Largest input is a 65 536-character name, so nothing goes in `Integration`. Every URL is drive-less (the random generator leaves out `:` so no `/X:` drive appears).

Oracle: curl 8.21.0 (Schannel build), run by hand on a ten-byte and an empty file: `-r 9-9` -> `9`; `-r 10-10`, `-r 10-`, `-C 10` -> empty, exit 0; `-r 11-`, `-C 11`, `-r 9223372036854775807-`, `-C 9223372036854775807` -> exit 36 `failed to resume file:// transfer`; `-r -9223372036854775807` -> exit 36 `Could not resume download`; `-r 9-9223372036854775807` -> `9`; empty file `-C 0` exit 0, `-C 1` exit 36; `d%2Fa%20b.txt` opened `d/a b.txt`; `d/%%41`, `d/a%4`, `d/%FF%FE`, `d/%C3%A9.txt` quoted as written in the exit 37 message. These were measured with `curl.exe` on `file://` URLs directly: `Record-CurlExchange.ps1` records loopback exchanges, and a `file://` transfer has no server side to record.

- **Boundaries:** `TryParse_EscapeCutShortAtTheEndOfThePath_KeepsOnlyACompleteEscapeAsAnEscape`, `TryParse_RootOnly_IsTheRootDirectory`, `TryParse_PathOfSixtyFourThousandCharacters_KeepsEveryCharacter`, `TryParse_PathOfTenThousandEscapes_DecodesEveryOne`, `TryParse_EscapeAtTheEdgeOfAscii_DecodesAsUtf8`, `ExecuteAsync_BoundedRangeAtTheLastByteAndBeyond_WritesWhatCurlWrites`, `ExecuteAsync_RangeFromOffsetAtOrPastTheLength_SucceedsEmptyOnlyAtTheLength`, `ExecuteAsync_ResumeFromTheLastByteOrTheLength_WritesTheRest`, `ExecuteAsync_ResumeFromLongMaxValue_ReportsFailedToResumeWithoutOverflowing`, `ExecuteAsync_SuffixRangeOfLongMaxValue_ReportsCouldNotResumeDownload`, `ExecuteAsync_SuffixRangeOfOne_WritesOnlyTheLastByte`, `ExecuteAsync_ResumeOnAnEmptyFile_SucceedsOnlyAtZero`, `ExecuteAsync_FileAtAndAroundTheChunkSize_WritesInWholeChunksThenTheRemainder`, `ExecuteAsync_BoundedRangeCrossingAChunkBoundary_WritesExactlyTheWindow`, `ExecuteAsync_VeryLongMissingName_ReportsExitThirtySevenWithTheWholeName`.
- **Malformed input:** `TryParse_PercentBeforeAnEscape_KeepsTheFirstPercentAndDecodesTheEscape`, `TryParse_EscapeWithANonHexDigit_KeepsItExactlyAsWritten`, `TryParse_EscapesThatAreNotUtf8_DecodeToReplacementCharacters`, `TryParse_Utf8SequenceCutByALiteralCharacter_DecodesEachHalfOnItsOwn`, `TryParse_Utf8SequenceSplitAcrossTwoEscapeRuns_IsNotJoined`, `TryParse_EncodedSlash_StaysAnEscapeInTheUrlPathAndBecomesASeparatorInTheOsPath`, `TryParse_EncodedDotDotAroundAnEncodedSlash_IsNotRemovedAsADotSegment`, `TryParse_LowercaseOrMixedCaseHexDigits_AreQuotedUppercase`, `TryParse_EncodedEuroSign_DecodesToTheCharacter`, `TryParse_SeededRandomPaths_NeverThrowAndKeepTheUrlPathAscii` (seed 1507, 2000 paths), `ExecuteAsync_EncodedSlashInThePath_OpensTheDecodedNestedPath`, `ExecuteAsync_MissingFileWithAMalformedOrNonAsciiName_QuotesTheEncodedPathAsCurlDoes`, `ExecuteAsync_NonAsciiName_OpensTheDecodedCharacters`, `ExecuteAsync_UploadToAnEncodedSlashPath_WritesTheDecodedNestedPath`.
- **Invalid partitions:** `TryParse_SchemeThatIsNotFile_ReturnsFalse` (http, ftp, files, fil), `TryParse_FileSchemeInMixedCase_IsAccepted`, `TryParse_UnescapedNonAsciiName_QuotesItsUtf8BytesAndOpensTheCharacters`, `TryParse_UrlPathOfANonAsciiName_IsAlwaysPureAscii`, `ExecuteAsync_UrlOfAnotherScheme_FailsWithUrlMalformatAndOpensNothing`, `ExecuteAsync_SourceOpenFailingWithAnyStatus_ReportsExitThirtySeven` (every `FileAccessStatus` failure), `ExecuteAsync_UploadDestinationOpenFailingWithAnyStatus_ReportsWriteError` (read-only destination included as `AccessDenied`), `ExecuteAsync_UploadOverAnExistingLongerFile_LeavesOnlyTheNewBytes`, `ExecuteAsync_EmptyUpload_CreatesAnEmptyDestination`. Hosts other than empty, `localhost` and `127.0.0.1` are refused by `CurlUrl` before this library and are already pinned in `FileUrlPathTests`.
- **State and concurrency:** `TryParse_SameUrlTwice_ReturnsEqualPaths`, `TryParse_ManyConcurrentCallers_AllGetTheSamePath`, `ExecuteAsync_SameHandlerTwice_WritesTheFileBothTimes`, `ExecuteAsync_AfterEachKindOfFailure_TheSameHandlerStillDownloads`, `ExecuteAsync_ManyConcurrentRangedDownloadsOnOneHandler_EachGetsItsOwnWindow` (32 tasks). Cancellation before, during and after the call, short reads and faulting streams are already attacked by the existing suite (`FileProtocolHandlerTests`, `CancellingStream`, `ShortReadStream`, `FaultingStream`), so they are not repeated.

Defects found: none. Every attack matched curl 8.21.0 or the documented contract, so no follow-up task was filed. One near miss noted, not filed: a suffix range on a source reporting a length of `long.MaxValue` would overflow `length + 1`; no file system can report such a file, so it is unreachable.

Test count: 386 before, 469 after (`dotnet test Curl.Protocol.File.UnitTests --filter "TestCategory!=Integration"`, 468 passed, 1 skipped as before); `dotnet build Curl.Protocol.File.UnitTests -warnaserror` clean, `dotnet format --verify-no-changes` clean.

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Curl.Protocol.File.UnitTests attacks FileUrlPath and FileProtocolHandler in all four families (386 -> 469 tests); no defects found
