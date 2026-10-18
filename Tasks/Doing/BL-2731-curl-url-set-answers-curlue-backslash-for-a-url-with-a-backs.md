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
completed:
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

- [ ] Each row listed answers as upstream wants through `Lib1560Checks.GetParts` over the real engine, pinned by a focused unit test in the fixed library's `.UnitTests` project.
- [ ] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 0 failing members for each changed library.

## Notes

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: bf4b3849 d6c1964a.
