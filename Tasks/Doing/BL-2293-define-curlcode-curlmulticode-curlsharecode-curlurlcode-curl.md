---
id: BL-2293
title: Define CurlCode, CurlMultiCode, CurlShareCode, CurlUrlCode, CurlHeaderCode and CurlFormAddCode in Curl.Api.UnitLibrary with upstream's numbers
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2292]
touches: [Curl.Api.UnitLibrary/Codes, Curl.Api.UnitTests/Codes]
requirement: none
created: 2026-10-15
completed:
---
# BL-2293 — Define CurlCode, CurlMultiCode, CurlShareCode, CurlUrlCode, CurlHeaderCode and CurlFormAddCode in Curl.Api.UnitLibrary with upstream's numbers

## Goal

`Curl.Api.UnitLibrary/Codes` (namespace `Curl.Api.Codes`) defines `CurlCode`, `CurlMultiCode`, `CurlShareCode`, `CurlUrlCode`, `CurlHeaderCode` and `CurlFormAddCode`, every member with upstream curl 8.22.0's number, and a conversion between `CurlCode` and `CurlExitCode`.

## Context

ADR-0506, "Names": `CurlCode` is a libcurl function's result (`CURLcode`), `CurlExitCode` (in `Curl.Protocol.Abstractions`) stays the engine's outcome and the curl tool's exit status; numbers are equal member for member. Sources (curl 8.22.0, the targeted version): `include/curl/curl.h` (`CURLE_*`, `CURLSHE_*`, `CURL_FORMADD_*`), `include/curl/multi.h` (`CURLM_*`), `include/curl/urlapi.h` (`CURLUE_*`), `include/curl/header.h` (`CURLHE_*`), at <https://github.com/curl/curl/tree/curl-8_22_0/include/curl>, and `docs/libcurl/libcurl-errors.md`. Member names are .NET casing of the upstream name (`CURLE_UNSUPPORTED_PROTOCOL` becomes `UnsupportedProtocol`); where `CurlExitCode` already names a member, use the same name. Obsolete upstream codes kept for their number (`CURLE_OBSOLETE*`) are included with `[Obsolete]`-free doc comments saying they are unused, so every number round-trips.

## Acceptance criteria

- [ ] The six enums exist in `Curl.Api.UnitLibrary/Codes`, each member documented with its upstream name and number.
- [ ] A test pins every member's number (a table test per enum) against the 8.22.0 headers.
- [ ] A test asserts that every `CurlExitCode` member has a `CurlCode` member with the same name and number and vice versa, and that the conversion in both directions preserves the number.
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
