---
id: BL-2462
title: Libcurl: SetOption for CURLOPT_CURLU over CurlUrlHandle
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2438, BL-2402]
touches: [Libcurl/Options/Network, Libcurl/CLAUDE.md, Libcurl.UnitTests/Options/Network, Curl.Api.UnitLibrary/Options/Network, Curl.Api.UnitTests/Options/Network]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2462 — Libcurl: SetOption for CURLOPT_CURLU over CurlUrlHandle

## Goal

`CURLOPT_CURLU` is settable: `SetOption(CurlOption.Curlu, curlUrlHandle)` takes a `Libcurl` `CurlUrlHandle` (BL-2438) and stores its engine `ICurlUrlHandle` on `EasyNetworkOptions`; `null` clears it.

## Context

Split from BL-2402, which set every other `EasyNetworkOptions` option in `Libcurl/Options/Network/EasyNetworkOptionSetters.cs`; `Curlu` is the one row it left out, because `CurlUrlHandle` does not exist until BL-2438. `EasyNetworkOptions.UrlFromUrlHandle` is a `string` today (BL-2295, written before `ICurlUrlHandle` existed), so the record needs an `ICurlUrlHandle? UrlHandle` (replacing or beside `UrlFromUrlHandle`; decide and record it in an ADR) so the engine can read the handle's URL at perform time, as libcurl does. Upstream: curl 8.22.0 `lib/setopt.c`, `case CURLOPT_CURLU`.

## Acceptance criteria

- [x] A test sets `CURLOPT_CURLU` to a `CurlUrlHandle` and finds its engine handle on `EasyNetworkOptions`; `null` clears it; any other object answers `BadFunctionArgument`.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl,Curl.Api.UnitLibrary` reports 0 failing members.
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` is kept true.

## Notes

- Decision (ADR-0588): `EasyNetworkOptions.UrlFromUrlHandle` (a string nothing read) is replaced by `ICurlUrlHandle? UrlHandle`, held by reference as libcurl holds the `CURLU *`, so the engine reads the URL at perform time.
- `SetUrlHandle` in `EasyNetworkOptionSetters`: a `CurlUrlHandle` stores its `EngineHandle`, `null` clears it, and any other object (a string, a bare engine handle) answers `BadFunctionArgument`.
- Added `Libcurl/CLAUDE.md` to touches (no task in Doing names it): the acceptance criteria require keeping it true.
- Measured: build clean, fast tests 0 failing projects (Libcurl.UnitTests 791, Curl.Api.UnitTests 429 passed), `Measure-CodeQuality.ps1 -Library Libcurl,Curl.Api.UnitLibrary` 0 failing members.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. CURLOPT_CURLU stores a CurlUrlHandle's engine ICurlUrlHandle as EasyNetworkOptions.UrlHandle; null clears it, other objects answer BadFunctionArgument
