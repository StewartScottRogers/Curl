---
id: BL-2952
title: Encode a whole URL's fragment with QUERY_NO under CURLU_URLENCODE, as curl 8.22.0's parseurl does
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/UrlHandleParts.cs, Curl.Engine.UnitTests/ManagedCurlUrlHandleTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2952 — Encode a whole URL's fragment with QUERY_NO under CURLU_URLENCODE, as curl 8.22.0's parseurl does

## Goal

`curl_url_set(CURLUPART_URL, "http://h/p#a?b c", CURLU_URLENCODE)` followed by a get of the fragment gives `a?b%20c`, as curl 8.22.0 does.

## Context

Found by BL-2926. In curl 8.22.0, `parseurl` in `lib/urlapi.c` encodes the fragment with `urlencode_str(..., TRUE, QUERY_NO)`, so a space is `%20` throughout. `Curl.Engine.UnitLibrary/UrlHandleParts.cs`'s `UrlEncoded` calls `UrlPartText.Encode(Fragment, query: false)`, which is QUERY_NOT_YET, so a `?` inside the fragment turns the spaces after it into `+`. The path already behaves correctly: a path never holds a `?`. To fix it, have `UrlEncoded` call `UrlPartText.UrlEncodeString(text, relative: true, UrlEncodeQuery.No)` for the path and the fragment, and `UrlEncodeQuery.Yes` for the query.

## Acceptance criteria

- [x] A test in `Curl.Engine.UnitTests/ManagedCurlUrlHandleTests.cs` sets `http://h/p#a?b c` under `CURLU_URLENCODE` and reads the fragment back as `a?b%20c`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and Curl.Engine.UnitLibrary keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 33d872f3 5bdfa932.
- 2026-10-21: Doing -> Done. UrlEncoded encodes the fragment and path with QUERY_NO, so a whole URL's fragment a?b c gives a?b%20c
