---
id: BL-2734
title: curl_url_set keeps an empty host under CURLU_NO_AUTHORITY and answers CURLUE_NO_HOST for https://::1/ (lib1560 rows)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-18
completed:
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

- [ ] Each row listed answers as upstream wants through `Lib1560Checks` (`GetParts`, `GetUrls`) over the real engine, pinned by a focused unit test in the fixed library's `.UnitTests` project.
- [ ] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 0 failing members for each changed library.

## Notes

## Log

- 2026-10-18: Created.
