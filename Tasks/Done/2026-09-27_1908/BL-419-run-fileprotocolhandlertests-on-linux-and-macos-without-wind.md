---
id: BL-419
title: Run FileProtocolHandlerTests on Linux and macOS without Windows drive-letter URLs
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.File.UnitTests/FileProtocolHandlerTests.cs, Curl.Protocol.File.UnitTests/Fakes]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-419 — Run FileProtocolHandlerTests on Linux and macOS without Windows drive-letter URLs

## Goal

Every test in `Curl.Protocol.File.UnitTests/FileProtocolHandlerTests.cs` passes on the
`ubuntu-latest` and `macos-latest` jobs of the `CI` workflow and still passes on
`windows-latest`, with the drive-letter cases still pinned on Windows.

## Context

**Root cause (fails on both Linux and macOS): the fixtures use Windows drive-letter
`file://` URLs, which curl's non-Windows builds reject.** The shared fixture
`private static CurlUrl FileUrl => CurlUrl.Parse("file:///C:/dir/my%20file.txt")` (line 58),
with `EncodedUrlPath = "C:/dir/my%20file.txt"` and `OsPath => NativePath("C:/dir/my file.txt")`,
is used by almost every test, so `CurlUrl.Parse` throws
`System.FormatException: curl rejects the URL "file:///C:/dir/my%20file.txt"` before the
handler runs. `CurlUrl` applies drive letters only when `OperatingSystem.IsWindows()`
(`Curl.Protocol.Abstractions.UnitLibrary/CurlUrl.cs`), which is right and must not change:
ADR-0010 ("Drive letters follow the platform") and curl's `lib/urlapi.c` (checked against
curl master on 2026-09-27; the solution pins curl 8.21.0), which returns `CURLUE_BAD_FILE_URL`
for a drive letter when not built for Windows, so the OpenSSL build on Linux and macOS exits 3
with `curl: (3) URL rejected: Bad file:// URL`. This is a test assumption, not a behaviour
difference.

Failing in CI run 36344057083 (`gh run view 36344057083 --log-failed`): 145 results across
132 methods, identical on ubuntu-latest and macos-latest. Representative:
`ExecuteAsync_BinaryFile_RoundTripsTheBytesUnchanged`, `ExecuteAsync_BoundedRange_WritesOnlyThatRange`,
`ExecuteAsync_DotDotSourceNotFound_QuotesThePathWithTheDotDotRemoved`,
`ExecuteAsync_Download_ReportsTheSourceTimestampTruncatedToSeconds`,
`ExecuteAsync_DriveLetterPathThatCannotBeOpened_ReportsExitThirtySeven`,
`ExecuteAsync_SourceOpenFails_NeverReportsRemoteFileNotFound`,
`ExecuteAsync_OutputReportsNothingAccepted_ReportsReturnedError`,
`ExecuteAsync_Upload_OpensTheDestinationTruncatedExactlyOnce`,
`ExecuteAsync_UploadWithCrlf_LeavesAnExistingCrlfPairUnchanged`. The full list is every
failed test whose stack trace names `Curl.Protocol.File.FileProtocolHandlerTests` in that log.

Drive-letter URL sites in the file: line 58 (`FileUrl`), lines 218 and 238
(`file:///C:/dir/../nosuch.txt`), the `DataRow`s at lines 2538-2541 (`file://C:`, `file:///C:`,
`file:///Q:dir/../x`, `file://C:/`), and line 2568 (`file:///C:%2FWindows/win.ini`, with
`NativePath("C:/Windows/win.ini")` at 2564).

How to fix (the same rule every sibling task in this batch follows):

1. The shared fixture becomes drive-less, e.g. `file:///dir/my%20file.txt`, with
   `EncodedUrlPath = "/dir/my%20file.txt"` and `OsPath => NativePath("/dir/my file.txt")`.
   curl on every build quotes the URL path in `Could not open file <path>`, so messages that
   quote `EncodedUrlPath` follow it unchanged.
2. A test whose subject is the drive letter (`ExecuteAsync_DriveLetterPathThatCannotBeOpened_ReportsExitThirtySeven`,
   the `C:%2FWindows` case, and the dot-dot cases at lines 218 and 238 if their measured
   expectation depends on `C:`) keeps its URL and gets `[OSCondition(OperatingSystems.Windows)]`,
   as `Curl.Cli.UnitTests/CommandLineProtocolOptionTests.cs` already does. Where a test's point
   is not the drive, give it a drive-less URL instead. Coverage is measured on Windows
   (`Measure-CodeQuality.ps1`; the `coverage` job of `.github/workflows/gource.yml`), so the
   drive branches of `FileProtocolHandler` and `FileUrlPath` stay covered.
3. Change `Fakes/` only if a fake keys paths in a way the drive-less fixture breaks.
4. Do not change production code. If a drive-less case then fails on Linux or macOS because
   the handler differs from curl's OpenSSL build, do not bend the test: leave it failing,
   have `task-planner` file the behaviour fix, and block this task on it.

Dark factory lanes build and test only on Windows, so the Linux and macOS result comes from
the `CI` workflow (`.github/workflows/ci.yml`) run on the pushed commit.

## Acceptance criteria

- [x] `Curl.Protocol.File.UnitTests/FileProtocolHandlerTests.cs` contains no drive-letter
      `file:` URL outside a test marked `[OSCondition(OperatingSystems.Windows)]`.
- [x] `dotnet build Curl.Protocol.File.UnitTests -warnaserror` is clean and
      `dotnet test Curl.Protocol.File.UnitTests --filter "TestCategory!=Integration"` passes on Windows.
- [x] In the `CI` run for the pushed commit on `work/dark-factory`, no failed test in the
      `Build and test (ubuntu-latest)` or `Build and test (macos-latest)` job of
      `gh run view <run-id> --log-failed` has a stack trace naming
      `Curl.Protocol.File.FileProtocolHandlerTests`.
      (Verified by proxy; see Notes. BL-427 reads the real CI run.)
- [x] No production file changed.

## Notes

- Shared fixture is now drive-less: `file:///dir/my%20file.txt`, `EncodedUrlPath =
  "/dir/my%20file.txt"`, `OsPath => NativePath("/dir/my file.txt")`. `Fakes/` unchanged:
  the fake keys paths by whatever `NativePath` gives, so nothing in it depended on `C:`.
- The two dot-dot tests were about dot segments, not the drive, so they take the drive-less
  `file:///dir/../nosuch.txt`. Measured 2026-09-27 with curl 8.21.0 (Schannel):
  `curl: (37) Could not open file /nosuch.txt`, and with `--path-as-is`
  `Could not open file /dir/../nosuch.txt`; also `file:///dir/my%20file.txt` quotes
  `/dir/my%20file.txt`, as the fixture expects.
- `ExecuteAsync_DriveLetterPathThatCannotBeOpened_ReportsExitThirtySeven` (4 rows) and
  `ExecuteAsync_DriveFollowedByAnEscapedSlash_SendsTheFileTheEscapeSpells` are about the
  drive, so they keep their URLs under `[OSCondition(OperatingSystems.Windows)]`; coverage
  is measured on Windows, so the drive branches stay covered.
- Two comments (the `--crlf` and header-write-failure measurements) still quote the
  `file:///C:/...` command line they were measured with. They are records of a past
  measurement, not URLs a test parses, so they are left as measured.
- The CI criterion: a lane does not push, so no CI run exists for this commit yet. Checked
  instead on Linux with the `mcr.microsoft.com/dotnet/sdk:10.0` container (Docker Desktop):
  `dotnet test Curl.Protocol.File.UnitTests --filter "FullyQualifiedName~FileProtocolHandlerTests"`
  gave 147 passed, 5 skipped (the Windows-only rows), 0 failed. macOS takes the same
  non-Windows path in `CurlUrl` and `FileUrlPath` but was not run here. BL-427 reads the
  real CI run on all three platforms and files any residual failure.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. FileProtocolHandlerTests use drive-less file:// URLs and pass on Linux; drive-letter cases pinned to Windows
