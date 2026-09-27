---
id: BL-422
title: Run the remaining Curl.Console tests that parse file:///C: URLs on Linux and macOS
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console.UnitTests/CurlCommandRunnerTests.cs, Curl.Console.UnitTests/CurlCommandRunnerStandardOutputFailureTests.cs, Curl.Console.UnitTests/TransferContextFactoryTests.cs, Curl.Console.UnitTests/CurlCommandRunnerWarningLineTests.cs, Curl.Console.UnitTests/CurlCommandRunnerTransferWarningTests.cs, Curl.Console.UnitTests/CurlCommandRunnerProxyContextTests.cs]
requirement: none
created: 2026-09-27
completed:
---
# BL-422 — Run the remaining Curl.Console tests that parse file:///C: URLs on Linux and macOS

## Goal

Every test in the six `Curl.Console.UnitTests` classes named below passes on the
`ubuntu-latest` and `macos-latest` jobs of the `CI` workflow and still passes on
`windows-latest`.

## Context

**Root cause (fails on both Linux and macOS): the tests pass Windows drive-letter `file://`
URLs, which curl's non-Windows builds reject.** Off Windows, `CurlUrl` rejects a drive letter
(`Curl.Protocol.Abstractions.UnitLibrary/CurlUrl.cs` passes `OperatingSystem.IsWindows()`), so
`CurlCommandRunner` stops with exit 3 and `curl: (3) URL rejected: Bad file:// URL`, and
`CurlUrl.Parse` in `TransferContextFactoryTests` throws
`System.FormatException: curl rejects the URL "file:///C:/x.txt"`. That production behaviour is
right and must not change: ADR-0010 ("Drive letters follow the platform") and curl's
`lib/urlapi.c` (checked against curl master on 2026-09-27; the solution pins curl 8.21.0), which
returns `CURLUE_BAD_FILE_URL` for a drive letter when not built for Windows, so the OpenSSL build
on Linux and macOS exits 3 with exactly that line. The sources are in-memory fakes
(`InMemoryFileSystem`, `RecordingProtocolHandler`), so no test needs a real `C:` path. This is
a test assumption, not a behaviour difference.

Drive-letter URL sites: `CurlCommandRunnerTests.cs` lines 202 and 383 (`file:///C:/nonexist/a`),
250 and 284 (`file:///C:/Windows/win.ini`), 663 (`file:///Z:/nx`), 723 (the helper's
`file:///C:/Windows/win.ini`); `CurlCommandRunnerStandardOutputFailureTests.cs` line 71
(`file:///C:/body.bin`); `TransferContextFactoryTests.cs` lines 20-90 (`file:///C:/x.txt`);
`CurlCommandRunnerWarningLineTests.cs` lines 51, 65, 79, 101; `CurlCommandRunnerTransferWarningTests.cs`
lines 59, 95, 111; `CurlCommandRunnerProxyContextTests.cs` line 49 (`file:///C:/source.txt`).
The `-o C:/...` output names are only names in the in-memory file system and may stay.

Failing in CI run 36344057083 (`gh run view 36344057083 --log-failed`), identical on
ubuntu-latest and macos-latest, 31 results:

- `CurlCommandRunnerTests` (8): `RunAsync_TwoFailures_PrintsBothLinesInOrderAndReturnsLast`,
  `RunAsync_OutputFileCannotBeCreated_ReturnsExit23WithCurlsWriteLine`,
  `RunAsync_OutputFileCannotBeCreatedWithoutSilent_PrintsFailedToOpenWarningBeforeWriteLine`,
  `RunAsync_OutputFileCannotBeCreatedUnderSilent_PrintsNoWarning`,
  `RunAsync_OutputFileCannotBeCreatedUnderSilentShowError_PrintsOnlyTheWriteLine`,
  `RunAsync_OutputFileOpenWarningAt79Columns_IsWrappedAsCurlWrapsIt`,
  `RunAsync_FailedTransferToOutputFile_DoesNotCreateTheFile`,
  `RunAsync_MoreOutputOptionsThanUrlsAndTheTransferFails_PrintsTheWarningAfterTheErrorLine`.
- `CurlCommandRunnerStandardOutputFailureTests` (7 results): `RunAsync_FileBodySmallerThanTheStdioBuffer_PrintsFailedWritingBody`,
  `RunAsync_FileBodyFillingTheStdioBuffer_PrintsCurlsPassedReturnedLine` (6 data rows).
- `TransferContextFactoryTests` (6): `Create_ClockGiven_IsTheContextsTimeProvider`, `Create_NoClock_IsTheSystemClock`,
  `Create_NoOptions_LeavesEveryOptionAtItsDefault`, `Create_NoProgress_IsNoTransferProgress`,
  `Create_PerTransferValues_AreCopiedAsGiven`, `Create_Progress_IsTheSinkGiven`.
- `CurlCommandRunnerWarningLineTests` (5 results): `RunAsync_FlagLikeOutputAndAFailedTransfer_PrintsTheWarningBeforeTheErrorLine`,
  `RunAsync_FlagLikeOutputOnAnAcceptedCommandLine_PrintsTheWarningBeforeTheTransfer`,
  `RunAsync_SilentAfterFlagLikeOutput_StillPrintsTheWarning`, `RunAsync_SilentBeforeFlagLikeOutput_PrintsNoWarning`.
- `CurlCommandRunnerTransferWarningTests` (3 results): `RunAsync_CaPathWarnings_ArePrintedBeforeTheHandlerRuns`,
  `RunAsync_SilentAnywhereWithCaPathWarnings_PrintsNoWarning`.
- `CurlCommandRunnerProxyContextTests` (2 results): `RunAsync_ProxyOptionForAFileUrl_ContextCarriesNoProxy`.

How to fix: replace each drive-letter URL with a drive-less one (`file:///nonexist/a`,
`file:///Windows/win.ini`, `file:///nx`, `file:///body.bin`, `file:///x.txt`,
`file:///source.txt`). curl quotes the URL path in `Could not open file <path>`, so
`RunAsync_TwoFailures_PrintsBothLinesInOrderAndReturnsLast` then expects
`curl: (37) Could not open file /nonexist/a` on every build. Leave comments that record a
Windows measurement as they are. The `--capath` tests pin Schannel-build warnings; they already
pass on Windows and the URL is their only Linux failure, so do not change their expected text
(if they then fail on Linux on the warning text itself, that is a separate behaviour question:
have `task-planner` file it and block this task on it). Do not change production code. Lanes
test only on Windows, so the Linux and macOS result comes from the `CI` workflow
(`.github/workflows/ci.yml`) run on the pushed commit.

## Acceptance criteria

- [ ] None of the six files contains a drive-letter `file:` URL in code (comments excepted).
- [ ] `dotnet build Curl.Console.UnitTests -warnaserror` is clean and
      `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes on Windows.
- [ ] In the `CI` run for the pushed commit on `work/dark-factory`, no failed test in the
      `Build and test (ubuntu-latest)` or `Build and test (macos-latest)` job of
      `gh run view <run-id> --log-failed` has a stack trace naming any of the six classes.
- [ ] No file outside the six named test files changed.

## Notes

## Log

- 2026-09-27: Created.
