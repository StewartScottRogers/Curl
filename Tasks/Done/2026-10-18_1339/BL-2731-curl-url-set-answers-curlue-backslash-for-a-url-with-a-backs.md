---
id: BL-2731
title: curl_url_set answers CURLUE_BACKSLASH for a URL with a backslash in its scheme separator or authority (lib1560 rows)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2731 — curl_url_set answers CURLUE_BACKSLASH for a URL with a backslash in its scheme separator or authority (lib1560 rows)

## Goal

`curl_url_set(CURLUPART_URL)` answers `CURLUE_BACKSLASH` (32) where libcurl 8.22.0 does, so lib1560's `get_parts` rows below pass.

## Context

Found by BL-2652's port of upstream lib1560 (`Curl.Conformance.LibTest.UnitLibrary/Lib1560Tables.GetParts.cs`, run by `Lib1560Checks.GetParts`). `curl_url_set` goes through `Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs` over `Curl.Protocol.Abstractions`' `CurlUrl`; upstream's check is in `lib/urlapi.c` (`parseurl`). Keep the command-line tool's URL handling as real curl has it.

Rows (`get_parts_list` index: input, flags, wanted, got):
- 0: `http:\\hostname`, `CURLU_GUESS_SCHEME`, 32, got 4.
- 1: `http:\\hostname:1234`, `CURLU_GUESS_SCHEME`, 32, got 4.
- 2: `http://hostname:1111\path`, none, 32, got 4.

## Acceptance criteria

- [x] Each row listed answers as upstream wants through `Lib1560Checks.GetParts` over the real engine, pinned by a focused unit test in the fixed library's `.UnitTests` project.
- [x] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 0 failing members for each changed library.

## Notes

- Fix: `CurlUrlAuthority.CheckPort` (split out of `SplitPort` to keep it under the complexity gate) answers the new `CurlUrlRejection.Backslash` when the first non-digit of the port text is a backslash, as curl 8.22.0's `Curl_parse_port` answers `CURLUE_BACKSLASH`; `ManagedCurlUrlHandle.RejectionCodes` maps it to `CurlUrlCode.Backslash` (32) and `CurlUrlRejectionMessages` gives curl's `Found a backslash where a forward slash was expected`. All three rows fail in the port check: rows 0 and 1 because `http:\\hostname` under `CURLU_GUESS_SCHEME` is read with no scheme (host `http`, port text `\\hostname`), row 2 at `1111\path`.
- Pinned by `ManagedCurlUrlHandleTests.Set_UrlWithABackslashWhereThePortEnds_ReturnsBackslash` (the three rows through the handle `curl_url_set` uses, which `Lib1560Checks.GetParts` calls and compares the code of) and `CurlUrlRejectionTests` rows.
- Tool side, measured on `/ucrt64/bin/curl` 8.22.0 Windows: `curl -sS "http:\\hostname"` and `curl "hostname:12\x"` now give Curl's tool the same `curl: (3) URL rejected: Found a backslash where a forward slash was expected` (before: the port message). `http://hostname:1111\path` and `http://h:1\x/` are accepted by the Windows tool (exit 6, resolving the host) but rejected by Curl, before and after this task (only the message changed); filed as BL-2742.
- Measure-CodeQuality: Curl.Engine.UnitLibrary 100/100, 0 failing; Curl.Protocol.Abstractions.UnitLibrary 100/100, 0 failing after the `CheckPort` split.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: bf4b3849 d6c1964a.
- 2026-10-18: Doing -> Done. curl_url_set answers CURLUE_BACKSLASH (32) for lib1560 get_parts rows 0-2: a backslash where a URL's port digits end
