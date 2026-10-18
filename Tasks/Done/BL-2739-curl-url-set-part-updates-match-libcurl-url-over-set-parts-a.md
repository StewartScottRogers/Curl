---
id: BL-2739
title: curl_url_set part updates match libcurl: url="" over set parts, an empty appended query, a scheme change dropping options (lib1560 rows)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs, Curl.Engine.UnitLibrary/UrlPartSetter.cs, Curl.Engine.UnitTests/ManagedCurlUrlHandleTests.Set.cs, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2739 — curl_url_set part updates match libcurl: url="" over set parts, an empty appended query, a scheme change dropping options (lib1560 rows)

## Goal

`curl_url_set` of single parts matches libcurl 8.22.0: `url=""` over a handle built from parts succeeds and keeps it, an empty query set with `CURLU_APPENDQUERY|CURLU_URLENCODE` leaves the URL without a trailing `?param=value&`, and changing an `imap://` URL's scheme to `https` drops its `;options`, so lib1560's `set_parts` rows below pass.

## Context

Found by BL-2652's port of upstream lib1560 (`Curl.Conformance.LibTest.UnitLibrary/Lib1560Tables.SetParts.cs`, run by `Lib1560Checks.SetParts`, whose `UpdateUrl` is upstream's `updateurl`). `curl_url_set` goes through `Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs`; upstream: `lib/urlapi.c` `curl_url_set` and `curl_url_get`'s `CURLUPART_URL`.

Rows (`set_parts_list` index: first URL, commands, set flags, wanted, got):
- 2: none, `scheme=https,host=example.com,path=/,url="",`, part code 0 and `https://example.com/`, got 3.
- 9: `https://example.com/?param=value`, `query="",`, `APPENDQUERY|URLENCODE`, `https://example.com/`, got `https://example.com/?param=value&`.
- 43: `imap://user:secret;optit@host/`, `scheme=https,`, `https://user:secret@host/`, got `https://user:secret;optit@host/`.

## Acceptance criteria

- [x] Each row listed answers as upstream wants through `Lib1560Checks.SetParts` over the real engine, pinned by a focused unit test in the fixed library's `.UnitTests` project.
- [x] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 0 failing members for each changed library.

## Notes

- 2026-10-18: touches narrowed from Curl.Engine.UnitLibrary, Curl.Engine.UnitTests to files, by reading ManagedCurlUrlHandle and UrlPartSetter, so it can run beside other Engine tasks.
- `ManagedCurlUrlHandle.SetUrl`: `url=""` is Ok and changes nothing when a get of the whole URL under the set flags succeeds, else MalformedInput (upstream `set_url`).
- `UrlPartSetter.SetQuery`: an empty query is stored as the empty query even under `CURLU_APPENDQUERY`, so no `&` is added and the URL reads back with no `?` (lib1560 row 9).
- `ManagedCurlUrlHandle.Login`: the whole URL leaves the options out under a supported scheme whose handler takes none (not imap, pop3, smtp or their TLS forms), as `curl_url_get` checks `PROTOPT_URLOPTIONS`; an unsupported scheme keeps them. `CURLUPART_OPTIONS` still reads them. A local scheme set, as `CurlUrlScheme` is internal to Abstractions, so Abstractions is not changed.
- Pinned by `ManagedCurlUrlHandleTests.Set.cs`; Measure-CodeQuality for Curl.Engine.UnitLibrary: 0 failing members.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b5b381b2 d6c1964a.
- 2026-10-18: Doing -> Done. url="" over set parts keeps them, an empty appended query leaves no ?, a scheme change to https drops imap options; pinned in ManagedCurlUrlHandleTests.Set.
