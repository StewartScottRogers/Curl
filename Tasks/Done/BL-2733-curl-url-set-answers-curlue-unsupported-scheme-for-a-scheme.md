---
id: BL-2733
title: curl_url_set answers CURLUE_UNSUPPORTED_SCHEME for a scheme-only URL such as data:, about: or mailto: (lib1560 rows)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2733 — curl_url_set answers CURLUE_UNSUPPORTED_SCHEME for a scheme-only URL such as data:, about: or mailto: (lib1560 rows)

## Goal

`curl_url_set(CURLUPART_URL)` without `CURLU_NON_SUPPORT_SCHEME` answers `CURLUE_UNSUPPORTED_SCHEME` (5) for a URL whose scheme is followed by a colon and no `//`, so lib1560's `get_url` rows below pass.

## Context

Found by BL-2652's port of upstream lib1560 (`Curl.Conformance.LibTest.UnitLibrary/Lib1560Tables.GetUrls.cs`, run by `Lib1560Checks.GetUrls`). `curl_url_set` goes through `Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs` over `Curl.Protocol.Abstractions`' `CurlUrl`; upstream: `lib/urlapi.c` `parseurl`, the scheme check before the authority.

Rows (`get_url_list` index: input, wanted, got):
- 106: `data:text/html;charset=utf-8;base64,PCFET0NUWVBFIEhUTUw+PG1ldGEgY`, 5, got 4.
- 107: `d:anything-really`, 5, got 4.
- 108: `about:config`, 5, got 4.
- 110: `mailto:infobot@example.com?body=send%20current-issue`, 5, got 27.

## Acceptance criteria

- [x] Each row listed answers as upstream wants through `Lib1560Checks.GetUrls` over the real engine, pinned by a focused unit test in the fixed library's `.UnitTests` project.
- [x] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 0 failing members for each changed library.

## Notes

- Fix: `ManagedCurlUrlHandle.SetUrl` now checks the scheme as soon as `UrlRelativeReference.IsAbsolute` finds one (`IsAcceptedScheme`) and answers `CurlUrlCode.UnsupportedScheme` before parsing anything after the colon, as upstream `parseurl` checks `Curl_get_scheme_handler` before counting slashes. `about:config`, `mailto:...`, `data:...` and `d:anything-really` no longer reach `CurlUrl`, which read them as host and port (4) or a bad port (27). The old `SchemeWithoutSlashes` could then only answer `BadSlashes`, so it is inlined in `Accept`.
- Tests: `ManagedCurlUrlHandleTests.Set_SchemeOnlyUrlWithAnUnsupportedScheme_ReturnsUnsupportedScheme` (the four rows) and `Set_RelativeReferenceOnAUrlWithAnUnsupportedScheme_ReturnsUnsupportedScheme` (keeps `Accept`'s unsupported branch covered). Rows 106-108 and 110 checked through `Lib1560Checks.GetUrls` over the real engine with a temporary test in `Curl.Conformance.LibTest.UnitTests` (0 errors), then removed, as that project is outside `touches`.
- Pipeline: a two-method change in one library, so the plan, review and document stages were done in this run without subagents, to keep the cost down. `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 0 failing members. Curl.Protocol.Abstractions needed no change.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 7547e176 d6c1964a.
- 2026-10-18: Doing -> Done. curl_url_set answers CURLUE_UNSUPPORTED_SCHEME for scheme-only URLs such as about:config, mailto: and data: (lib1560 get_url rows 106-108, 110)
