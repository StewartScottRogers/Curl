---
id: BL-2740
title: curl_url_set of a blank or fragment-only redirect keeps the base URL as libcurl does, and http://?hi under CURLU_NO_AUTHORITY (lib1560 rows)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs, Curl.Engine.UnitLibrary/UrlRelativeReference.cs, Curl.Engine.UnitTests/ManagedCurlUrlHandleTests.Set.cs, Curl.Engine.UnitTests/UrlRelativeReferenceTests.cs, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-18
completed:
---
# BL-2740 — curl_url_set of a blank or fragment-only redirect keeps the base URL as libcurl does, and http://?hi under CURLU_NO_AUTHORITY (lib1560 rows)

## Goal

`curl_url_set(CURLUPART_URL)` over a handle that holds a URL resolves a blank redirect to the base URL without its fragment, keeps an empty query when the redirect is fragment-only, and takes `http://?hi` under `CURLU_NO_AUTHORITY`, as libcurl 8.22.0 does, so lib1560's `set_url` rows below pass.

## Context

Found by BL-2652's port of upstream lib1560 (`Curl.Conformance.LibTest.UnitLibrary/Lib1560Tables.SetUrls.cs`, run by `Lib1560Checks.SetUrls`). The relative case goes through `Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs` and `UrlRelativeReference.Resolve`; upstream: `lib/urlapi.c` `redirect_url`.

Rows (`set_url_list` index: base, redirect, set flags, wanted, got):
- 0 to 9: each base with redirect `""`, such as `https://example.com` wanting `https://example.com/` and `http://example.com/path#frag` wanting `http://example.com/path`; got 3 (`CURLUE_MALFORMED_INPUT`).
- 44: `http://host/path?`, `#new`, `http://host/path?#new`, got `http://host/path#new` (read back with `CURLU_GET_EMPTY`).
- 45: `http://host/path?#`, `#new`, `http://host/path?#new`, got `http://host/path#new`.
- 59: `http://user:foo@example.com/path?query#frag`, `http://?hi`, `NO_AUTHORITY`, `http:///?hi`, got 14.

## Acceptance criteria

- [ ] Each row listed answers as upstream wants through `Lib1560Checks.SetUrls` over the real engine, pinned by a focused unit test in the fixed library's `.UnitTests` project.
- [ ] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 0 failing members for each changed library.

## Notes

- 2026-10-18: touches narrowed from Curl.Engine.UnitLibrary, Curl.Engine.UnitTests to files, by reading ManagedCurlUrlHandle and UrlRelativeReference.Resolve, so it can run beside other Engine tasks.

## Log

- 2026-10-18: Created.
