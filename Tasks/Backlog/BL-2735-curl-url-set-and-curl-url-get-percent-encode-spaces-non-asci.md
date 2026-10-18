---
id: BL-2735
title: curl_url_set and curl_url_get percent-encode spaces, non-ASCII and reserved bytes under CURLU_URLENCODE as libcurl does (lib1560 rows)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-18
completed:
---
# BL-2735 — curl_url_set and curl_url_get percent-encode spaces, non-ASCII and reserved bytes under CURLU_URLENCODE as libcurl does (lib1560 rows)

## Goal

`curl_url_set` and `curl_url_get` percent-encode spaces, non-ASCII bytes and reserved characters exactly where libcurl 8.22.0 does (`CURLU_URLENCODE`, `CURLU_ALLOW_SPACE`, the URL's own path and fragment, redirects), and keep a set path's `%XX` as given but upper-cased, so lib1560's rows below pass.

## Context

Found by BL-2652's port of upstream lib1560 (`Curl.Conformance.LibTest.UnitLibrary/Lib1560Tables.*.cs`, run by `Lib1560Checks`). The URL API goes through `Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs` (`UrlPartText`) over `Curl.Protocol.Abstractions`' `CurlUrl`; upstream: `lib/urlapi.c` `urlencode_str` and `curl_url_set`'s `CURLU_URLENCODE` path.

Rows (input, flags, wanted, got):
- `get_parts_list` 36: `https://curl.se/#  `, `URLENCODE|ALLOW_SPACE`, fragment `%20%20`, got the spaces.
- `get_parts_list` 47: `https://user@example.net?hello# space `, `ALLOW_SPACE|URLENCODE`, fragment `%20space%20`, got ` space `.
- `get_parts_list` 97: `https://user:password@example.net/ge t?this=and-what`, path `/ge%20t`, got `/ge t`.
- `get_url_list` 7: `https://example.com/räksmörgås`, `https://example.com/r%C3%A4ksm%C3%B6rg%C3%A5s`, got it unencoded.
- `set_url_list` 49: redirect `../there/tes t case=/%TESTNUMBER0002? yes no` (see the table), wanted `http://example.net/there/tes%20t%20case=/%TESTNUMBER0002?+yes+no`, got the spaces.
- `set_parts_list` 0: `path=one /$!$&'()*+;=:@{}[]%,` with `URLENCODE`, wanted `https://example.com/one%20/$!$&'()*+;=:@{}[]%25`, got every reserved character encoded.
- `set_parts_list` 35: `path=%4A%4b%4C,`, wanted `https://host/%4A%4B%4C`, got `%4a%4b%4c`.

## Acceptance criteria

- [ ] Each row listed answers as upstream wants through `Lib1560Checks` (`GetParts`, `GetUrls`, `SetUrls`, `SetParts`) over the real engine, pinned by a focused unit test in the fixed library's `.UnitTests` project.
- [ ] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 0 failing members for each changed library.

## Notes

## Log

- 2026-10-18: Created.
