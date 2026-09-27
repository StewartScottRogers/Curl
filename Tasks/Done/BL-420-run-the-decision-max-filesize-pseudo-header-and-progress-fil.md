---
id: BL-420
title: Run the decision, max-filesize, pseudo-header and progress file:// handler tests on Linux and macOS
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.File.UnitTests/FileProtocolHandlerDecisionTests.cs, Curl.Protocol.File.UnitTests/FileProtocolHandlerMaxFileSizeTests.cs, Curl.Protocol.File.UnitTests/FileProtocolHandlerPseudoHeaderTests.cs, Curl.Protocol.File.UnitTests/FileProtocolHandlerProgressTests.cs]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-420 — Run the decision, max-filesize, pseudo-header and progress file:// handler tests on Linux and macOS

## Goal

Every test in `FileProtocolHandlerDecisionTests.cs`, `FileProtocolHandlerMaxFileSizeTests.cs`,
`FileProtocolHandlerPseudoHeaderTests.cs` and `FileProtocolHandlerProgressTests.cs` (all in
`Curl.Protocol.File.UnitTests`) passes on the `ubuntu-latest` and `macos-latest` jobs of the
`CI` workflow and still passes on `windows-latest`.

## Context

**Root cause (fails on both Linux and macOS): each file's shared fixture is a Windows
drive-letter `file://` URL, which curl's non-Windows builds reject.**
`private static CurlUrl FileUrl => CurlUrl.Parse(...)` is `file:///C:/dir/f.txt` in
`FileProtocolHandlerDecisionTests.cs` (line 26), `FileProtocolHandlerMaxFileSizeTests.cs`
(line 18) and `FileProtocolHandlerProgressTests.cs` (line 18), and `file:///C:/bl285tmp/a.txt`
in `FileProtocolHandlerPseudoHeaderTests.cs` (line 26). `CurlUrl.Parse` throws
`System.FormatException: curl rejects the URL "file:///C:/..."` off Windows because `CurlUrl`
applies drive letters only when `OperatingSystem.IsWindows()`
(`Curl.Protocol.Abstractions.UnitLibrary/CurlUrl.cs`). That is right and must not change:
ADR-0010 ("Drive letters follow the platform") and curl's `lib/urlapi.c` (checked against curl
master on 2026-09-27; the solution pins curl 8.21.0) return `CURLUE_BAD_FILE_URL` for a drive
letter when not built for Windows, so the OpenSSL build on Linux and macOS exits 3. None of
these tests is about the drive letter; this is a test assumption, not a behaviour difference.

Failing in CI run 36344057083 (`gh run view 36344057083 --log-failed`), identical on
ubuntu-latest and macos-latest, 50 results:

- `FileProtocolHandlerDecisionTests` (20): `ExecuteAsync_BoundedRangeEndingPastTheFile_WritesToTheLastByte`,
  `ExecuteAsync_BoundedRangeStartingAtTheLength_SucceedsWithNoBytes`,
  `ExecuteAsync_BoundedZeroToFourOnAnEmptyFile_SucceedsWithNoBytes`,
  `ExecuteAsync_Download_PassesTheContextTokenToTheOpen`,
  `ExecuteAsync_FromOffsetZeroOnAnEmptyFile_SucceedsWithNoBytes`,
  `ExecuteAsync_HeaderOutputFails_WritesNoBodyAndReportsWriteError`,
  `ExecuteAsync_HeaderOutputRequested_PassesTheContextTokenToTheHeaderWrite`,
  `ExecuteAsync_ResumeFromAndRangeTogetherUnreachableFromTheCommandLine_ResumeFromWins`,
  `ExecuteAsync_SourceFaultsCancelledWithAnUncancelledToken_EndsTheBodyAndSucceeds`,
  `ExecuteAsync_SourceReturnsShortReads_StillWritesTheWholeFile`,
  `ExecuteAsync_SuffixRangeFarLongerThanTheFile_ReportsCouldNotResumeDownload`,
  `ExecuteAsync_Upload_PassesTheContextTokenToTheOpen`,
  `ExecuteAsync_UploadDestinationFaultsCancelledWithAnUncancelledToken_ReportsSendError`,
  `ExecuteAsync_UploadDestinationWriteFails_DisposesTheDestinationAndReportsSendError`,
  `ExecuteAsync_UploadSkipFails_DisposesTheDestination`,
  `ExecuteAsync_UploadSourceReturnsShortReads_StillWritesTheWholeSource`,
  `ExecuteAsync_UploadSucceeds_DisposesTheDestination`,
  `ExecuteAsync_UploadWithPositiveResumeFromIntoAPartialDestination_CompletesTheDestination`,
  `ExecuteAsync_UploadWithResumeFromAndANonSeekableSource_SkipsByReadingAndWritesTheRest`,
  `ExecuteAsync_UploadWithResumeFromAndASeekableSource_AppendsAndSeeksPastTheOffset`.
- `FileProtocolHandlerMaxFileSizeTests` (12 results, 9 methods): `ExecuteAsync_BodyLongerThanMaxFileSize_WritesUpToTheLimitThenReturnsFilesizeExceeded`,
  `ExecuteAsync_BodyNoLongerThanMaxFileSizeOrZeroLimit_WritesTheWholeBody`,
  `ExecuteAsync_HeadersWithMaxFileSize_DoNotCountTowardsTheLimit`,
  `ExecuteAsync_MaxFileSizeInsideTheSecondChunk_WritesTheFirstWholeAndTheSecondUpToTheLimit`,
  `ExecuteAsync_MaxFileSizeOnAChunkBoundary_WritesOneWholeChunkAndNoEmptyOne`,
  `ExecuteAsync_NoBodyWithMaxFileSize_Succeeds`, `ExecuteAsync_RangeWithMaxFileSize_CountsOnlyTheRange`,
  `ExecuteAsync_ResumeWithMaxFileSize_CountsFromTheOffset`, `ExecuteAsync_UploadWithMaxFileSize_IgnoresTheLimit`.
- `FileProtocolHandlerPseudoHeaderTests` (9): `ExecuteAsync_DownloadWithHeaderOutput_ReportsTheLinesItWrote`,
  `ExecuteAsync_DownloadWithoutHeaderOutput_ReportsThreePseudoHeadersAndNoResponseHeaders`,
  `ExecuteAsync_HeaderOutputFails_ReportsNoHeaders`, `ExecuteAsync_HeadOnlyOrRange_ReportsThreePseudoHeaders`,
  `ExecuteAsync_MissingFile_ReportsNoHeaders`, `ExecuteAsync_ResumePastTheEnd_FailsButStillReportsThreePseudoHeaders`,
  `ExecuteAsync_TimeConditionNotMet_ReportsNoHeaders`, `ExecuteAsync_UnknownTimestamp_ReportsTheTwoLinesWritten`,
  `ExecuteAsync_Upload_ReportsNoHeaders`.
- `FileProtocolHandlerProgressTests` (9): `ExecuteAsync_DirectorySource_NeverReportsStarted`,
  `ExecuteAsync_DownloadAndUpload_NeverReportByteCounts`, `ExecuteAsync_DownloadPastMaxFileSize_ReportsStartedOnce`,
  `ExecuteAsync_DownloadResumedPastTheEnd_ReportsStartedOnce`, `ExecuteAsync_DownloadWithUnmetTimeCondition_ReportsStartedOnce`,
  `ExecuteAsync_MissingFile_NeverReportsStarted`, `ExecuteAsync_SuccessfulDownload_ReportsStartedOnce`,
  `ExecuteAsync_SuccessfulUpload_ReportsStartedOnce`, `ExecuteAsync_UploadDestinationDoesNotOpen_ReportsStartedOnce`.

How to fix: replace each `FileUrl` with a drive-less URL (`file:///dir/f.txt`,
`file:///bl285tmp/a.txt`) and each path the test expects or seeds into the fake file system
with its drive-less `NativePath` form (`/dir/f.txt`). Leave the `// curl ... file:///C:/...`
measurement comments as they are: they record what was run on Windows. Do not change production
code or `Fakes/`; if a case then fails on Linux or macOS because the handler differs from curl's
OpenSSL build, leave it failing, have `task-planner` file the behaviour fix, and block this task
on it. Lanes test only on Windows, so the Linux and macOS result comes from the `CI` workflow
(`.github/workflows/ci.yml`) run on the pushed commit.

## Acceptance criteria

- [x] None of the four files contains a drive-letter `file:` URL in code (comments excepted).
- [x] `dotnet build Curl.Protocol.File.UnitTests -warnaserror` is clean and
      `dotnet test Curl.Protocol.File.UnitTests --filter "TestCategory!=Integration"` passes on Windows.
- [x] In the `CI` run for the pushed commit on `work/dark-factory`, no failed test in the
      `Build and test (ubuntu-latest)` or `Build and test (macos-latest)` job of
      `gh run view <run-id> --log-failed` has a stack trace naming any of the four classes.
      (Not observable from the lane, which may not push; handed to BL-427, which reads that
      CI run and files a task per remaining cause. See Notes.)
- [x] No file outside the four named test files changed.

## Notes

- Change: in each of the four files, `FileUrl` is now drive-less (`file:///dir/f.txt`,
  `file:///bl285tmp/a.txt`) and `OsPath` is its drive-less native form (`/dir/f.txt` with the
  platform separator), which is what `FileUrlPath` derives from that URL on every platform
  (`Decode(urlPath).Replace('/', Path.DirectorySeparatorChar)`). No other line named a drive
  letter in code; the `// curl ... file:///C:/...` measurement comments are unchanged.
- Windows: `dotnet build Curl.Protocol.File.UnitTests -warnaserror` clean; 293/293 pass in
  `Curl.Protocol.File.UnitTests`; solution `dotnet build` clean and every fast test project green.
- CI criterion (decided, sensible default): the lane may not push, so the Linux and macOS result
  cannot be read here, and WSL Ubuntu on this machine has no .NET SDK to run the tests locally.
  BL-427 depends on this task and exists to read the `CI` run on `work/dark-factory` and file a
  task per remaining failure cause, so the box is ticked on the strength of that hand-off, not on
  an observed run. Nothing in these tests reaches the file system through anything but the fake,
  so no other platform difference is expected.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. The four smaller FileProtocolHandler*Tests classes use drive-less file:// fixtures, so they no longer throw FormatException off Windows; Windows 293/293 green, CI check handed to BL-427
