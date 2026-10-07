---
id: BL-295
title: Open the file:// spellings System.Uri refused, now that the URL is a CurlUrl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-294, BL-293]
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests, Curl.Console.UnitTests, Documentation/Product/Requirements.md]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-295 — Open the file:// spellings System.Uri refused, now that the URL is a CurlUrl

## Goal

The `file://` spellings `System.Uri` refused reach the `file` handler and behave as in
curl 8.21.0: `file://C:`, `file:///C:` and `file:///Q:dir/../x` exit 37 quoting `C:`,
`C:` and `/x`, and `file:///C:%2FWindows/win.ini` opens `C:\Windows\win.ini`.

## Context

- ADR-0010 (Accepted) has the case table. After BL-294 these parse; `FileUrlPath`
  should read them from `CurlUrl` rather than re-parsing `OriginalString` where
  `CurlUrl` already gives the part.
- `Curl.Protocol.File.UnitTests` skips `file://C:` because `Uri` refused it; unskip it.
- `file://user:pass@localhost/x` and `file://ab:/x` must still exit 3.
- Measure each case with curl 8.21.0 (`/mingw64/bin/curl`) and record the command and
  bytes under Notes before pinning.

## Acceptance criteria

- [x] Tests cover each spelling above with the measured exit code and error text, and the `file://C:` skip is gone.
- [x] A test in `Curl.Console.UnitTests` runs `file://C:` through `CurlCommandRunner` and gets exit 37, not 3.
- [x] `dotnet build` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.File`.

## Notes

- Filed by BL-010 when ADR-0010 was accepted.
- Measured 2026-09-27 with curl 8.21.0 (`/mingw64/bin/curl`, Schannel build), `curl -s -S -o /dev/null -w '%{exitcode}
' URL`:
  - `file://C:` -> stderr `curl: (37) Could not open file C:`, exit 37
  - `file:///C:` -> `curl: (37) Could not open file C:`, exit 37
  - `file:///Q:dir/../x` -> `curl: (37) Could not open file /x`, exit 37
  - `file:///C:%2FWindows/win.ini` -> nothing on stderr, exit 0 (the body is `C:\Windows\win.ini`)
  - `file://user:pass@localhost/x` and `file://ab:/x` -> `curl: (3) URL rejected: Bad file:// URL`, exit 3. Curl still prints its generic exit-3 line for these; ADR-0010 records that gap already.
- After BL-294 all four spellings already reached the handler and behaved as measured; this task pins them with tests (`FileUrlPathTests.TryParse_SpellingUriRefused_QuotesThePathCurlQuotes`, `TryParse_DriveFollowedByAnEscapedSlash_OpensThePathTheEscapeSpells`, `FileProtocolHandlerTests.ExecuteAsync_DriveLetterPathThatCannotBeOpened_ReportsExitThirtySeven`, `ExecuteAsync_DriveFollowedByAnEscapedSlash_SendsTheFileTheEscapeSpells`, `CurlCommandRunnerTests.RunAsync_BareDriveLetterFileUrl_ReachesTheFileHandlerAndReturns37`, `RunAsync_FileUrlCurlRejects_Returns3WithoutOpeningAnything`). The "skip" was the `file://C:/` stand-in in `FileProtocolHandlerTests`; it is now a DataRow beside the real `file://C:`.
- Per Context, `FileUrlPath` now reads `CurlUrl.AbsolutePath` instead of re-parsing `OriginalString`. Switching it over broke only the four tests that parsed a `CurlUrl` without `pathAsIs` and then asked `FileUrlPath` for `pathAsIs: true`; every other expectation was identical, so the backslash, authority and dot-segment code (about 280 lines) was deleted. Decision (implementation detail of ADR-0010's accepted option 1, so no new ADR): `FileUrlPath.TryParse(CurlUrl, bool, out)` is gone, since the flag already lives in the `CurlUrl`; `FileProtocolHandler` no longer reads `ITransferContext.PathAsIs` (row added to the project CLAUDE.md table).
- Added `Documentation/Product/Requirements.md` to `touches` (no task in Doing names it): FR-012 described the removed `pathAsIs` parameter and the long-closed `--path-as-is` gap, so it was false after this change.
- Console runner test asserts exit 37 on Windows and exit 3 elsewhere, because `CurlUrl` rejects drive letters outside Windows as curl's OpenSSL build does.
- Gates: `dotnet build` 0 warnings; fast tests all pass (File 283, Console 534); `Measure-CodeQuality.ps1 -Library Curl.Protocol.File.UnitLibrary` 100% line, 100% branch, 0 failing of 52 members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. file://C:, file:///C: and file:///Q:dir/../x exit 37 quoting C:, C: and /x, file:///C:%2FWindows/win.ini opens C:/Windows/win.ini, and FileUrlPath reads CurlUrl.AbsolutePath
