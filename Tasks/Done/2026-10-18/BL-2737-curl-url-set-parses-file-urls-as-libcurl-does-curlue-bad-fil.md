---
id: BL-2737
title: curl_url_set parses file: URLs as libcurl does: CURLUE_BAD_FILE_URL for file:/ and file:foo, Windows drive paths kept (lib1560 rows)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2737 — curl_url_set parses file: URLs as libcurl does: CURLUE_BAD_FILE_URL for file:/ and file:foo, Windows drive paths kept (lib1560 rows)

## Goal

The URL API parses `file:` URLs as libcurl 8.22.0 does: `file:/` and `file:` followed by a relative path, `?` or `#` answer `CURLUE_BAD_FILE_URL` (19), and on Windows a drive path keeps its backslashes, so lib1560's rows below pass.

## Context

Found by BL-2652's port of upstream lib1560 (`Curl.Conformance.LibTest.UnitLibrary/Lib1560Tables.*.cs`, run by `Lib1560Checks`). `curl_url_set` goes through `Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs` over `Curl.Protocol.Abstractions`' `CurlUrl`; upstream: `lib/urlapi.c` `parse_file`. The Windows rows are upstream's `#ifdef _WIN32` rows (`WindowsOnly` in the port); pin each platform's answer in its own test.

Rows (input, wanted, got):
- `get_parts_list` 105, 106 (Windows): `file://C:\programs\foo`, `file:///C:\programs\foo` with `DEFAULT_SCHEME`, path `C:\programs\foo`, got `C:/programs/foo`.
- `get_parts_list` 132: `file:/`, 19, got 0.
- `get_url_list` 189 to 193: `file:./`, `file:foo`, `file:foo/bar`, `file:?q`, `file:#f`, 19, got 4.

## Acceptance criteria

- [x] Each row listed answers as upstream wants through `Lib1560Checks` (`GetParts`, `GetUrls`) over the real engine, pinned by focused unit tests in the fixed library's `.UnitTests` project (each platform's answer in its own test where they differ).
- [x] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 0 failing members for each changed library.

## Notes

- Measured real curl 8.22.0 (Windows Schannel): `file:/`, `file:foo`, `file:./`, `file:?q` and `file:#f` all exit 3, and `-w %{url_effective}` of `file://C:Windowswin.ini` shows forward slashes, so the tool converts backslashes while libcurl keeps them.
- Decision: `CurlUrlParser` now rejects `file:/` (too short) and a file path that starts with neither `/` nor a drive letter, for the tool and the URL API alike. The URL API goes through the new `CurlUrl.TryParseForUrlApi` (`CurlUrlApiFlags`), which keeps a file URL's backslashes and, without `CURLU_GUESS_SCHEME` or `CURLU_DEFAULT_SCHEME`, reads `file:` with no slash as a file URL, as `Curl_is_absolute_url` does when not guessing; the tool always guesses, so `file:foo` still reads as a host there.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 35fb7d52 d6c1964a.
- 2026-10-18: Doing -> Done. file: URLs parse as libcurl 8.22.0 does in the URL API; lib1560 rows pass; build clean, fast tests green, quality 0 failing
