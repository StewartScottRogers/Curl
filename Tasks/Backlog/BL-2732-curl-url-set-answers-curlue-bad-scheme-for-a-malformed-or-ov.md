---
id: BL-2732
title: curl_url_set answers CURLUE_BAD_SCHEME for a malformed or over-long scheme (lib1560 rows)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-18
completed:
---
# BL-2732 — curl_url_set answers CURLUE_BAD_SCHEME for a malformed or over-long scheme (lib1560 rows)

## Goal

`curl_url_set(CURLUPART_URL)` answers `CURLUE_BAD_SCHEME` (27) for a scheme libcurl 8.22.0 refuses (not starting with a letter, holding a space, or longer than `MAX_SCHEME_LEN`), so lib1560's rows below pass.

## Context

Found by BL-2652's port of upstream lib1560 (`Curl.Conformance.LibTest.UnitLibrary/Lib1560Tables.*.cs`, run by `Lib1560Checks`). `curl_url_set` goes through `Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs` over `Curl.Protocol.Abstractions`' `CurlUrl`; upstream's check is `Curl_is_absolute_url` in `lib/urlapi.c`. Today Curl answers 4 (`CURLUE_BAD_PORT_NUMBER`), parsing the scheme as a host and port.

Rows (input, flags, wanted, got):
- `get_parts_list` 39 to 42: `1h://example.net`, `..://example.net`, `-ht://example.net`, `+ftp://example.net`, none, 27, got 4.
- `get_parts_list` 92: `htt ps://user:password@example.net/get?this=and-what`, 27, got 4.
- `get_url_list` 146: `AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA://hostname/path` (41-letter scheme), 27, got 4.
- `huge` URL 0: a 119,998-byte scheme with `CURLU_NON_SUPPORT_SCHEME`, 27, got another code.

## Acceptance criteria

- [ ] Each row listed answers as upstream wants through `Lib1560Checks` (`GetParts`, `GetUrls`, `Huge`) over the real engine, pinned by a focused unit test in the fixed library's `.UnitTests` project.
- [ ] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 0 failing members for each changed library.

## Notes

## Log

- 2026-10-18: Created.
