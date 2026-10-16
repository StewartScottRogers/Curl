---
id: BL-2436
title: Engine: ICurlUrlHandle get over CurlUrl for every part and flag
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2456]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2436 — Engine: ICurlUrlHandle get over CurlUrl for every part and flag

## Goal

The engine's `ICurlUrlHandle` gets URL, scheme, user, password, options, host, port, path, query, fragment and zone id, with `CURLU_DEFAULT_PORT`, `URLDECODE`, `URLENCODE`, `PUNYCODE`, `NO_DEFAULT_PORT`, `GET_EMPTY` and the rest, built on Curl's existing `CurlUrl` parser.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] A test per part and per flag, with upstream's `CurlUrlCode` results for missing parts.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Plan: `ManagedCurlUrlHandle` (the engine's `ICurlUrlHandle`) over `UrlHandleParts` (one nullable field per upstream part), read by per-part readers with `UrlPartText` for the URLDECODE, URLENCODE, PUNYCODE and PUNY2IDN transforms, following curl 8.22.0's `curl_url_get` (ADR-0601). `ManagedCurlEngine.CreateUrl` now returns it.
- A get needs a parsed URL, so `Set(CurlUrlPart.Url, ...)` parses a whole URL here (DEFAULT_SCHEME, GUESS_SCHEME else BadScheme, PATH_AS_IS, DISALLOW_USER, each `CurlUrlRejection` mapped to its `CurlUrlCode`). Other parts, clearing, relative URLs and `Duplicate` throw `NotSupportedException` and are left to BL-2437.
- Default: `CurlUrl` hides whether the port was written and whether the scheme was guessed, so both are recovered from the URL text (`UrlHandleParts.Of`); this keeps `Curl.Protocol.Abstractions`, outside `touches`, unchanged. A scheme's default port is the one `CurlUrl` gives `scheme://localhost/`.
- Default: a `file` URL is rebuilt as `file://` + path + query + fragment, as 8.22.0 does.
- Not measured against real curl: libcurl's URL API is not reachable through the curl tool or `Record-CurlExchange.ps1`; behaviour follows upstream's `lib/urlapi.c`. `PassingUpstreamCases.txt` unchanged; Curl.Conformance.UnitTests green.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10. Fast tests all green (Curl.Engine.UnitTests 1185 passed).
- The plan document's URL section matches what was built; not edited (outside `touches`).

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. The engine's ICurlUrlHandle parses a URL and gets every part with every CURLU_* get flag, upstream's CURLUE_NO_* codes for missing parts
