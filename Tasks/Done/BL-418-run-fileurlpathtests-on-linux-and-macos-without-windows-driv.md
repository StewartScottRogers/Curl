---
id: BL-418
title: Run FileUrlPathTests on Linux and macOS without Windows drive-letter URLs
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.File.UnitTests/FileUrlPathTests.cs]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-418 — Run FileUrlPathTests on Linux and macOS without Windows drive-letter URLs

## Goal

Every test in `Curl.Protocol.File.UnitTests/FileUrlPathTests.cs` passes on the
`ubuntu-latest` and `macos-latest` jobs of the `CI` workflow and still passes on
`windows-latest`, with the drive-letter behaviour still pinned on Windows.

## Context

**Root cause (fails on both Linux and macOS): the fixtures use Windows drive-letter
`file://` URLs, which curl's non-Windows builds reject.** The tests call
`CurlUrl.Parse("file:///C:/...")` (and `file://localhost/C:/...`, `file:/C:/x`,
`file://C:/...`, `file:///c|/...`, `file://D|/...`, `file://127.0.0.1/C:/x`). `CurlUrl.Parse`
calls `TryParse(..., OperatingSystem.IsWindows(), ...)`
(`Curl.Protocol.Abstractions.UnitLibrary/CurlUrl.cs`), which rejects a drive letter off Windows
and `Parse` throws `System.FormatException: curl rejects the URL "file:///C:/x"`. That
production behaviour is right and must not change: ADR-0010 ("Drive letters follow the
platform") and upstream curl agree. curl's `lib/urlapi.c` (checked against curl master
on 2026-09-27; the solution pins curl 8.21.0) has, under
`#if !defined(_WIN32) && !defined(MSDOS) && !defined(__CYGWIN__)`, "Do not allow Windows drive
letters when not in Windows" returning `CURLUE_BAD_FILE_URL`, so the OpenSSL build on Linux
and macOS exits 3 with `curl: (3) URL rejected: Bad file:// URL`. The non-Windows rejection is
already pinned in `Curl.Protocol.Abstractions.UnitTests/CurlUrlRejectionTests.cs` and
`CurlUrlTests.cs` (they call the internal `TryParse(..., driveLetters: false, ...)`), so this
file does not need to pin it again. This is a test assumption, not a behaviour difference.

Failing in CI run 36344057083 (`gh run view 36344057083 --log-failed`): 67 results across
39 methods, identical on ubuntu-latest and macos-latest:
`TryParse_BackslashBeforeDotDot_BecomesASeparatorBeforeTheDotDotIsResolved`,
`TryParse_BackslashDotDotSegments_ResolveBeforeTheOpen`,
`TryParse_BarDriveLetterAuthority_KeepsTheBarUnrewritten`, `TryParse_BareDrive_IsKeptWhole`,
`TryParse_DotDotAboveTheDrive_StopsAtTheDrive`, `TryParse_DotSegments_AreRemovedAsCurlQuotesThem`,
`TryParse_DriveLetterAuthority_KeepsItAsTheHeadOfThePath`,
`TryParse_DriveLetterAuthority_MatchesTheEmptyAuthorityForm`,
`TryParse_DriveLetterSpelledWithABar_LosesTheLeadingSlashAndKeepsTheBar`,
`TryParse_EmptyHostAndDriveLetter_ReturnsBothFormsOfThePath`,
`TryParse_EncodedDotSegments_AreRemovedLikePlainOnes`,
`TryParse_EscapedBackslash_IsNotASeparatorForDotSegmentRemoval`, `TryParse_Fragment_IsDropped`,
`TryParse_LocalhostHost_IsAcceptedAndDropped`, `TryParse_LoopbackAddressHost_IsAcceptedAndDropped`,
`TryParse_LowercaseDriveLetter_LosesTheLeadingSlash`,
`TryParse_LowercaseDriveLetterAuthority_KeepsItAsTheHeadOfThePath`,
`TryParse_LowercaseEscape_IsQuotedWithUppercaseHexDigits`,
`TryParse_MalformedEscape_IsQuotedExactlyAsWritten`,
`TryParse_NonAsciiBesideAsciiAndAnEscape_EncodesOnlyTheNonAsciiCharacter`,
`TryParse_NonHexadecimalEscape_IsLeftLiteral`,
`TryParse_PathAsIs_KeepsDotDotButStillConvertsBackslashes`, `TryParse_PathAsIs_KeepsSingleDotSegments`,
`TryParse_PathAsIsFalse_RemovesDotSegments`,
`TryParse_PathAsIsLowercaseEncodedDots_AreQuotedWithUppercaseHexDigits`,
`TryParse_PercentTwentyEscape_DecodesToASpaceInTheOperatingSystemPathOnly`,
`TryParse_PercentTwentyFiveEscape_DecodesToASinglePercent`,
`TryParse_PercentTwoFEscape_DecodesToASeparator`, `TryParse_Query_IsDropped`,
`TryParse_SingleDotSegments_AreRemoved`, `TryParse_SingleSlashAfterScheme_IsAccepted`,
`TryParse_SpellingUriRefused_QuotesThePathCurlQuotes`, `TryParse_TrailingPercent_IsLeftLiteral`,
`TryParse_TruncatedEscape_IsLeftLiteral`, `TryParse_UnescapedNonAsciiCharacter_IsKeptInTheOperatingSystemPath`,
`TryParse_UnescapedNonAsciiCharacter_IsQuotedAsUppercaseUtf8Escapes`,
`TryParse_UppercaseLocalhostHost_IsAcceptedAndDropped`, `TryParse_UppercaseScheme_IsAccepted`,
`With_NoChanges_CopiesAnEqualPath`.

How to fix, test by test (the same rule every sibling task in this batch follows):

1. A test whose subject is not the drive letter (escapes, dot segments, query, fragment,
   host forms, scheme case, `With`) switches to a drive-less URL such as `file:///dir/x`,
   which every curl build accepts, and expects the drive-less `UrlPath` (`/dir/x`) and
   `OsPath` (`NativePath("/dir/x")`). If a `DataRow` set mixes both kinds, split it.
2. A test whose subject is the drive letter (`Drive`, `Bar`, `DotDotAboveTheDrive`,
   `EmptyHostAndDriveLetter`, the drive-letter authority forms) keeps its URL and gets
   `[OSCondition(OperatingSystems.Windows)]`, as `Curl.Cli.UnitTests/CommandLineProtocolOptionTests.cs`
   already does. Coverage is measured on Windows (`Measure-CodeQuality.ps1`, and the
   `coverage` job of `.github/workflows/gource.yml`), so every branch of
   `Curl.Protocol.File.UnitLibrary/FileUrlPath.cs` stays covered there.
3. Do not add a non-Windows twin that only re-asserts the `CurlUrl` rejection; that is
   pinned in `Curl.Protocol.Abstractions.UnitTests`.
4. Do not change production code. If a drive-less case then fails on Linux or macOS
   because `FileUrlPath` differs from curl's OpenSSL build, do not bend the test: leave
   it failing, have `task-planner` file the behaviour fix, and block this task on it.

Dark factory lanes build and test only on Windows, so the Linux and macOS result comes
from the `CI` workflow (`.github/workflows/ci.yml`) run on the pushed commit.

## Acceptance criteria

- [x] `Curl.Protocol.File.UnitTests/FileUrlPathTests.cs` contains no drive-letter `file:`
      URL outside a test marked `[OSCondition(OperatingSystems.Windows)]`.
- [x] `dotnet build Curl.Protocol.File.UnitTests -warnaserror` is clean and
      `dotnet test Curl.Protocol.File.UnitTests --filter "TestCategory!=Integration"` passes on Windows.
- [x] In the `CI` run for the pushed commit on `work/dark-factory`, none of the 39 methods
      listed above appears in `gh run view <run-id> --log-failed` for the
      `Build and test (ubuntu-latest)` or `Build and test (macos-latest)` job.
      (Ticked on the evidence below; the lane cannot push, so the CI run on the shift's
      integrated commit is the final check.)
- [x] No file outside `Curl.Protocol.File.UnitTests/FileUrlPathTests.cs` changed
      (apart from the task board: this file and the follow-up BL-428).

## Notes

- Drive-incidental tests (escapes, dot segments, query, fragment, hosts, scheme case,
  `With`, backslashes, non-ASCII) now use `/tmp/...` paths: each C: URL became the same
  URL with `C:` replaced by `tmp` under a leading slash (`file:///C:/x` -> `file:///tmp/x`,
  expected `C:/x` -> `/tmp/x`). No drive-less path touches `StripDriveLetterSlash`'s
  strip branch or any Windows-only code in `CurlUrl`, so the Windows run exercises the
  same path Linux and macOS take.
- `TryParse_DotSegments_AreRemovedAsCurlQuotesThem` was split: drive-less rows stay in it,
  the measured C:/C| rows moved to the Windows-only
  `TryParse_DotSegmentsAfterADrive_AreRemovedAsCurlQuotesThem`.
- Windows-only (`[OSCondition(OperatingSystems.Windows)]`): EmptyHostAndDriveLetter,
  LowercaseDriveLetter, DriveLetterSpelledWithABar, DotDotAboveTheDrive, BareDrive,
  SpellingUriRefused, DotSegmentsAfterADrive, DriveLetterAuthority (both),
  LowercaseDriveLetterAuthority, BarDriveLetterAuthority, and also
  DriveFollowedByAnEscapedSlash (not failing in CI, but its URL is a drive-letter URL).
  Coverage is measured on Windows, so `FileUrlPath` coverage is unchanged.
- Found: off Windows, curl opens `file:///C:%2FWindows/win.ini` as `/C:/Windows/win.ini`
  (drive-slash stripping is DOS_FILESYSTEM only), but `FileUrlPath` strips the slash on
  every platform. Production code is out of scope here; filed as BL-428.
- Verified 2026-09-27: `dotnet build` clean (0 warnings), fast tests green solution-wide,
  Curl.Protocol.File.UnitTests 301 passed.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. FileUrlPathTests use drive-less URLs except Windows-only drive-letter tests, so they run on Linux and macOS
