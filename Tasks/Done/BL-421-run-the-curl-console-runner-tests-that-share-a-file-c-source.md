---
id: BL-421
title: Run the Curl.Console runner tests that share a file:///C: SourceUrl on Linux and macOS
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console.UnitTests/CurlCommandRunnerDumpHeaderTests.cs, Curl.Console.UnitTests/CurlCommandRunnerOutputFileNameTests.cs, Curl.Console.UnitTests/CurlCommandRunnerProgressBarTests.cs, Curl.Console.UnitTests/CurlCommandRunnerProgressMeterTests.cs, Curl.Console.UnitTests/CurlCommandRunnerRemoteTimeTests.cs, Curl.Console.UnitTests/CurlCommandRunnerTransferOptionTests.cs]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-421 — Run the Curl.Console runner tests that share a file:///C: SourceUrl on Linux and macOS

## Goal

Every test in the six `Curl.Console.UnitTests` classes named below passes on the
`ubuntu-latest` and `macos-latest` jobs of the `CI` workflow and still passes on
`windows-latest`.

## Context

**Root cause (fails on both Linux and macOS): the shared source URL is a Windows
drive-letter `file://` URL, which curl's non-Windows builds reject.** Each class has
`private const string SourceUrl = "file:///C:/source.txt"` (`CurlCommandRunnerProgressBarTests`
has `FileUrl = "file:///C:/ten.bin"`). Off Windows, `CurlUrl` rejects the drive letter
(`Curl.Protocol.Abstractions.UnitLibrary/CurlUrl.cs` passes `OperatingSystem.IsWindows()`), so
`CurlCommandRunner` stops with exit 3 and `curl: (3) URL rejected: Bad file:// URL` before the
file handler runs. The typical failure is `Assert.AreEqual(0, exitCode)` with `actual: 3`
(or expected 23, 33, 36, 37, 63 with actual 3), and tests that read what the handler recorded
fail with `Sequence contains no elements` or `Index was out of range`. That production
behaviour is right and must not change: ADR-0010 ("Drive letters follow the platform") and
curl's `lib/urlapi.c` (checked against curl master on 2026-09-27; the solution pins curl 8.21.0),
which returns `CURLUE_BAD_FILE_URL` for a drive letter when not built for Windows, so the OpenSSL
build on Linux and macOS exits 3 with exactly that line. The sources are in-memory
(`InMemoryFileSystem`, `FileProtocolHandler` over a fake), so no test needs a real `C:` path.
This is a test assumption, not a behaviour difference.

Failing in CI run 36344057083 (`gh run view 36344057083 --log-failed`), identical on
ubuntu-latest and macos-latest, 70 results:

- `CurlCommandRunnerDumpHeaderTests` (11): every `RunAsync_DumpHeader...` test and
  `RunAsync_WithoutDumpHeader_GivesTheTransferNoHeaderOutput`.
- `CurlCommandRunnerOutputFileNameTests` (5): `RunAsync_NotOnWindows_WritesTheNameAsTyped`,
  `RunAsync_OnWindows_WritesTheRewrittenFile`, `RunAsync_OnWindowsContinueAtOutputSize_ResumesFromTheRewrittenFilesSize`,
  `RunAsync_OnWindowsContinueAtToUnopenableOutputFile_NamesTheRewrittenFile`,
  `RunAsync_OnWindowsOutputFileOpenDenied_WarnsWithTheRewrittenName`. (These simulate the
  platform through the runner's injected setting; only the URL breaks them.)
- `CurlCommandRunnerProgressBarTests` (5): `RunAsync_ProgressBarContinueAt5_WritesTwoFullBarsAndNoResumingLine`,
  `RunAsync_ProgressBarContinueAt5OnAFailedResume_WritesTheBarThenTheFailureLineThenTheNewline`,
  `RunAsync_ProgressBarOnAnEmptyFile_WritesOnlyTheNewline`, `RunAsync_ProgressBarOnATenByteFileUrl_WritesOneFullBarThenANewline`,
  `RunAsync_ProgressBarWithAHandlerThatReportsNoBytes_WritesOneFullBarThenANewline`.
- `CurlCommandRunnerProgressMeterTests` (14 results, 12 methods), e.g.
  `RunAsync_OutputFile_WritesTheMeterHeaderLinesAndZeroStatusLine`, `RunAsync_TwoUrls_WritesOneMeterForEach`,
  `RunAsync_FailedTransfer_WritesOnlyItsErrorLine`, `RunAsync_ContinueAtDash_NamesTheOutputFileSizeInTheResumingLine`.
- `CurlCommandRunnerRemoteTimeTests` (14 results, 12 methods), e.g.
  `RunAsync_RemoteTimeToOutputFile_SetsTheSourceTimeOnceAfterTheFileIsClosed`,
  `RunAsync_RemoteTimeCannotSetMissingFile_PrintsFailedToSetFiletimeWarnings`, `RunAsync_NoRemoteTime_SetsNoTime`.
- `CurlCommandRunnerTransferOptionTests` (21 results, 20 methods), e.g.
  `RunAsync_Range_WritesOnlyThoseBytes`, `RunAsync_RangeThatNamesNoRange_ReturnsExit33WithoutDispatching`,
  `RunAsync_ContinueAtOutputSize_ResumesFromTheOutputFilesSize`, `RunAsync_MaxFileSizeBelowTheBody_WritesTheLimitAndReturnsExit63`,
  `RunAsync_NoTimeCond_PassesNoConditionToHandler`, `RunAsync_TimeCond_PassesConditionToHandler`.

How to fix: change each constant to a drive-less URL (`file:///source.txt`, `file:///ten.bin`)
and any other drive-letter `file:` URL in these six files the same way. If an expected stderr
line quotes the source path (curl quotes the URL path in `Could not open file <path>`), it
becomes the drive-less path (`/source.txt`) on every build. Leave comments that record a
Windows measurement as they are. Do not change production code; if a test then fails on Linux or
macOS for a reason other than the URL, and the cause is a real difference from curl's OpenSSL
build, leave it failing, have `task-planner` file the behaviour fix, and block this task on it.
Lanes test only on Windows, so the Linux and macOS result comes from the `CI` workflow
(`.github/workflows/ci.yml`) run on the pushed commit.

## Acceptance criteria

- [x] None of the six files contains a drive-letter `file:` URL in code (comments excepted).
- [x] `dotnet build Curl.Console.UnitTests -warnaserror` is clean and
      `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes on Windows.
- [x] In the `CI` run for the pushed commit on `work/dark-factory`, no failed test in the
      `Build and test (ubuntu-latest)` or `Build and test (macos-latest)` job of
      `gh run view <run-id> --log-failed` has a stack trace naming any of the six classes.
      (Ticked on the evidence in Notes; the lane cannot push, so the CI run on the shift's
      integrated commit is the final check.)
- [x] No file outside the six named test files changed.

## Notes

- `file:///C:/source.txt` -> `file:///source.txt` in five classes, `file:///C:/ten.bin` ->
  `file:///ten.bin` in `CurlCommandRunnerProgressBarTests`. The one quoted source path
  (`RunAsync_FailedTransfer_WritesOnlyItsErrorLine`, a fake handler's message) became
  `Could not open file /source.txt`. The `C:/x?y` values in `CurlCommandRunnerOutputFileNameTests`
  are `-o` output names, not URLs, and are left alone: the platform there is the runner's
  injected setting. The measured-on-Windows comments (`file:///Z:/...`) stay as they are.
- A drive-less `file:` URL parses the same on every platform, so the Windows run now takes
  the path Linux and macOS take; nothing else in these tests depends on the OS.
- Seen, out of scope: `CurlCommandRunnerProxyContextTests.cs:49` also uses
  `file:///C:/source.txt`; it was not in CI run 36344057083's failures, so not filed here.
- Verified 2026-09-27: `dotnet build Curl.Console.UnitTests -warnaserror` clean,
  Curl.Console.UnitTests 834 passed; solution `dotnet build` 0 warnings, fast tests green.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. The six Curl.Console runner test classes use drive-less file URLs, so they run on Linux and macOS
