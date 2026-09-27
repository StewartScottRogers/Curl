---
id: BL-428
title: Keep the leading slash of a /X: path in FileUrlPath off Windows
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-428 — Keep the leading slash of a /X: path in FileUrlPath off Windows

## Goal

Off Windows, `FileUrlPath` keeps the leading `/` of a URL path that starts `/X:` or `/X|`, so `file:///C:%2FWindows/win.ini` opens the absolute path `/C:/Windows/win.ini` exactly as the Linux/macOS (OpenSSL) curl build does, while Windows behaviour is unchanged.

## Context

- `Curl.Protocol.File.UnitLibrary/FileUrlPath.cs`, `TryParse(CurlUrl, out FileUrlPath)` calls the private `StripDriveLetterSlash(url.AbsolutePath)`, which drops the slash in front of `/X:` or `/X|` on every platform.
- Upstream curl only does that on Windows: the stripping in `lib/file.c` (`file_connect`) sits inside `#ifdef DOS_FILESYSTEM`. Confirm against the lib/file.c source of the curl version the solution matches (state the version checked in the test or a code comment).
- On Linux/macOS the OpenSSL build rejects a real drive prefix (letter, `:` or `|`, then `/` or end of path) with exit 3 (`CURLE_URL_MALFORMAT`); `CurlUrl` already mirrors that through the `driveLetters` parameter of `CurlUrl.TryParse(string, bool pathAsIs, bool driveLetters, out CurlUrl)` (`Curl.Protocol.Abstractions.UnitLibrary/CurlUrl.cs`). But it accepts URLs whose drive letter is not followed by `/`, e.g. `file:///C:%2FWindows/win.ini` or `file://localhost/Q:dir/x`, and opens them as absolute paths `/C:/Windows/win.ini` and `/Q:dir/x`. Our `FileUrlPath` currently yields `C:/Windows/win.ini` (a relative path) off Windows.
- Found during BL-418, which made `Curl.Protocol.File.UnitTests/FileUrlPathTests.cs` cross-platform; `TryParse_DriveFollowedByAnEscapedSlash_OpensThePathTheEscapeSpells` is now Windows-only.
- Fix: make the stripping platform-dependent by passing or injecting the platform flag the same way `CurlUrl.TryParse` takes `driveLetters` (e.g. a `bool driveLetters` overload of `FileUrlPath.TryParse`, with the existing entry point supplying `OperatingSystem.IsWindows()`), so both branches are unit-testable on any OS and 100% coverage holds. Do not change `Curl.Protocol.Abstractions.UnitLibrary`.
- Tests for the non-Windows branch must run on every OS (no `OperatingSystem` guard, no `TestCategory=Integration`).

## Acceptance criteria

- [ ] A test in `Curl.Protocol.File.UnitTests/FileUrlPathTests.cs` parses `file:///C:%2FWindows/win.ini` with drive letters off and asserts `UrlPath` is `/C:%2FWindows/win.ini` and `OsPath` starts with the directory separator (`/C:/Windows/win.ini` with `/` separators); it runs on every OS.
- [ ] A test parses `file://localhost/Q:dir/x` with drive letters off and asserts the path keeps its leading `/` (`/Q:dir/x`); it runs on every OS.
- [ ] A test with drive letters on still strips the slash (`file:///C:/Windows/win.ini` gives `C:/Windows/win.ini`), and every existing `FileUrlPathTests` test passes unchanged in intent.
- [ ] `dotnet build Curl.Protocol.File.UnitLibrary -warnaserror` and `dotnet build Curl.Protocol.File.UnitTests -warnaserror` are clean.
- [ ] `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] `FileUrlPath` has 100% line and branch coverage from `Curl.Protocol.File.UnitTests`.

## Notes

## Log

- 2026-09-27: Created.
