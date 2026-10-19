---
id: BL-2784
title: Answer lib1560 huge()'s 120000-byte-part URLs as curl does (URL 1 to 6 fail to parse)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-18
completed:
---
# BL-2784 — Answer lib1560 huge()'s 120000-byte-part URLs as curl does (URL 1 to 6 fail to parse)

## Goal

`ManagedCurlUrlHandle.Set(CurlUrlPart.Url, ...)` under `CURLU_NON_SUPPORT_SCHEME` answers `Ok` for each lib1560 `huge()` URL whose 119,998-byte part is the user, password, host, path, query or fragment (URLs 1 to 6), and `BadScheme` for URL 0, as curl 8.22.0 does.

## Context

Found by BL-2783 running upstream case 1560 with every group forced to run (pop3, smtp and imap added to `UpstreamCurlPlatform` and `Lib1560.RunGroups` made not to stop, both local edits, reverted): stdout prints `URL 1: failed to parse [c://aaaa...]` to `URL 6`, from `Lib1560Checks.Huge` (`Lib1560Checks.Handles.cs`), with or without BL-2783's fix. Measure which code each URL answers first, then fix it in `Curl.Engine.UnitLibrary` (`ManagedCurlUrlHandle.cs` or `UrlHandleParts`). Needed before BL-2741 can put 1560 on the ratchet.

## Acceptance criteria

- [ ] A `Curl.Engine.UnitTests` test sets each of huge()'s seven URLs and pins `BadScheme` for URL 0 and `Ok` with the big part read back whole for URLs 1 to 6.
- [ ] With every lib1560 group forced to run, `CURL_UPSTREAM_CASE=1560` prints no `URL n: failed to parse` line.
- [ ] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports no failing member in `Curl.Engine.UnitLibrary`.

## Notes

## Log

- 2026-10-18: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 2493c688 37fccf5f 4887efa2.
