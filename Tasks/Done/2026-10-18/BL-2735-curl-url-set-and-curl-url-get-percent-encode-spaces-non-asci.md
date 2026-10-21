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
completed: 2026-10-18
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

- [x] Each row listed answers as upstream wants through `Lib1560Checks` (`GetParts`, `GetUrls`, `SetUrls`, `SetParts`) over the real engine, pinned by a focused unit test in the fixed library's `.UnitTests` project.
- [x] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 0 failing members for each changed library.

## Notes

- Three changes in `Curl.Engine.UnitLibrary`, all following curl 8.22.0's `lib/urlapi.c`; no ADR, since each is upstream's own behaviour, pinned by upstream's own lib1560 rows:
  - Parsing a whole URL under `CURLU_URLENCODE` encodes its path, query and fragment as `parseurl` does with `urlencode_str` (`UrlHandleParts.UrlEncoded`, over `UrlPartText.Encode`): a space is `%20`, or `+` in the query, and control and non-ASCII bytes are `%XX`. This fixes get_parts 36, 47 and 97, get_url 7 and set_url 49.
  - Setting a path under `CURLU_URLENCODE` keeps upstream's `allowed_in_path` bytes (`! $ & ' ( ) { } [ ] * + , ; = : @ /`) as well as the unreserved ones (set_parts 0).
  - A set without `CURLU_URLENCODE` now upper-cases its `%xx` escapes (8.22.0) where it used to lower-case them (set_parts 35). Two existing tests that pinned lower case were changed to match.
- Checked over the real `ManagedCurlEngine` with a throwaway file-based app (since deleted) that ran each lib1560 row through `Lib1560Checks` on its own. All seven rows pass. The rows still failing are host, file, fragment-clearing and other rows outside this task: get_parts 10,14,38,52,105,106,132; get_url 0-3,123,124,136,178,179,189-193; set_url 0-9,44,45; set_parts 2,7,9,23,39,43.
- Test1560 itself is skipped by the ratchet harness ("Curl lacks the feature pop3"), so it cannot be run through `CURL_UPSTREAM_CASE`.
- `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 0 failing members.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 7b0c5032 d6c1964a.
- 2026-10-18: Doing -> Done. CURLU_URLENCODE parse encodes path, query and fragment; path set keeps allowed_in_path; %xx upper-cased; lib1560 rows pass
