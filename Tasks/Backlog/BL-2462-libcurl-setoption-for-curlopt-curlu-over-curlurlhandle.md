---
id: BL-2462
title: Libcurl: SetOption for CURLOPT_CURLU over CurlUrlHandle
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2438, BL-2402]
touches: [Libcurl/Options/Network, Libcurl.UnitTests/Options/Network, Curl.Api.UnitLibrary/Options/Network, Curl.Api.UnitTests/Options/Network]
requirement: none
created: 2026-10-15
completed:
---
# BL-2462 — Libcurl: SetOption for CURLOPT_CURLU over CurlUrlHandle

## Goal

`CURLOPT_CURLU` is settable: `SetOption(CurlOption.Curlu, curlUrlHandle)` takes a `Libcurl` `CurlUrlHandle` (BL-2438) and stores its engine `ICurlUrlHandle` on `EasyNetworkOptions`; `null` clears it.

## Context

Split from BL-2402, which set every other `EasyNetworkOptions` option in `Libcurl/Options/Network/EasyNetworkOptionSetters.cs`; `Curlu` is the one row it left out, because `CurlUrlHandle` does not exist until BL-2438. `EasyNetworkOptions.UrlFromUrlHandle` is a `string` today (BL-2295, written before `ICurlUrlHandle` existed), so the record needs an `ICurlUrlHandle? UrlHandle` (replacing or beside `UrlFromUrlHandle`; decide and record it in an ADR) so the engine can read the handle's URL at perform time, as libcurl does. Upstream: curl 8.22.0 `lib/setopt.c`, `case CURLOPT_CURLU`.

## Acceptance criteria

- [ ] A test sets `CURLOPT_CURLU` to a `CurlUrlHandle` and finds its engine handle on `EasyNetworkOptions`; `null` clears it; any other object answers `BadFunctionArgument`.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Libcurl,Curl.Api.UnitLibrary` reports 0 failing members.
- [ ] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` is kept true.

## Notes

## Log

- 2026-10-15: Created.
