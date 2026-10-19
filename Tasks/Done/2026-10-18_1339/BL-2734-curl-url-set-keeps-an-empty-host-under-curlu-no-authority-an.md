---
id: BL-2734
title: curl_url_set keeps an empty host under CURLU_NO_AUTHORITY and answers CURLUE_NO_HOST for https://::1/ (lib1560 rows)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2734 — curl_url_set keeps an empty host under CURLU_NO_AUTHORITY and answers CURLUE_NO_HOST for https://::1/ (lib1560 rows)

## Goal

`curl_url_set(CURLUPART_URL)` keeps an empty host under `CURLU_NO_AUTHORITY` (the path after `scheme://` staying the path) and answers `CURLUE_NO_HOST` (14) for `https://::1/`, so lib1560's rows below pass.

## Context

Found by BL-2652's port of upstream lib1560 (`Curl.Conformance.LibTest.UnitLibrary/Lib1560Tables.*.cs`, run by `Lib1560Checks`). `curl_url_set` goes through `Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs` over `Curl.Protocol.Abstractions`' `CurlUrl`; upstream: `lib/urlapi.c` `parse_authority` and `hostname_check`.

Rows (input, flags, wanted, got):
- `get_parts_list` 3: `weird:///path`, `NON_SUPPORT_SCHEME|NO_AUTHORITY`, parts `weird | [11] | [12] | [13] |  | [15] | /path | [16] | [17]`, got host `path` and path `/`.
- `get_parts_list` 4: `weird:////path`, same flags, path `//path`, got 28 (`CURLUE_BAD_SLASHES`).
- `get_url_list` 214: `custom-scheme://?expected=test-new-good`, same flags, `custom-scheme:///?expected=test-new-good`, got 14.
- `get_url_list` 14: `https://::1/`, none, 14, got 4.

## Acceptance criteria

- [x] Each row listed answers as upstream wants through `Lib1560Checks` (`GetParts`, `GetUrls`) over the real engine, pinned by a focused unit test in the fixed library's `.UnitTests` project.
- [x] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 0 failing members for each changed library.

## Notes

- Upstream (`lib/urlapi.c`, curl 8.22.0): under `CURLU_NO_AUTHORITY` `parse_scheme` skips at most two slashes, the rest starting the path, and `parseurl` accepts an empty host; `parse_authority` replaces a port error with `CURLUE_NO_HOST` when the host is empty. Real curl 8.22.0 (`curl.exe -sS "https://::1/"`, `"http://:x/"`, `"http://user@:80@host/"`) exits 3 with no host for all three.
- `Curl.Protocol.Abstractions`: `CurlUrl.TryParseAllowingEmptyHost` (public, `CURLU_NO_AUTHORITY`) over `CurlUrlParser.Parse`'s new `allowEmptyHost`; `CurlUrlAuthority.ParseHostAndPort` answers `NoHost` for an empty host before any port error. Tests: `CurlUrlAllowingEmptyHostTests`, rows in `CurlUrlRejectionTests`, `CurlUrlAdversarialTests.TryParse_WithAnEmptyHostBeforeABadPort_RejectsItAsNoHost`.
- `Curl.Engine`: `ManagedCurlUrlHandle.TryParseUnder` picks the parse by flags; `UrlHandleParts.Of` keeps an empty host for every scheme but `file`. Tests: the four rows in `ManagedCurlUrlHandleTests.Set.cs`. The four rows were also run through `Lib1560Checks.GetParts`/`GetUrls` over the real engine with a scratch test (passed, then removed).
- Added `Curl.Console.UnitTests` to `touches`: `CurlCommandRunnerAdversarialCommandLineTests` pinned 8.21.0's bad-port text for `http://user@:80@host/`, which 8.22.0 rejects as no host. No task in Doing named it. The pinned text is Curl's current NoHost message; curl 8.22.0 prints `No host present` there, filed as BL-2746.
- `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary,Curl.Engine.UnitLibrary`: 0 failing members. No ADR: no design choice, each change follows upstream's code.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 254af4d6 d6c1964a.
- 2026-10-18: Doing -> Done. curl_url_set keeps an empty host under CURLU_NO_AUTHORITY and answers CURLUE_NO_HOST for https://::1/; the four lib1560 rows pass
