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
completed: 2026-10-15
---
# BL-2293 — Define CurlCode, CurlMultiCode, CurlShareCode, CurlUrlCode, CurlHeaderCode and CurlFormAddCode in Curl.Api.UnitLibrary with upstream's numbers

## Goal

`Curl.Api.UnitLibrary/Codes` (namespace `Curl.Api.Codes`) defines `CurlCode`, `CurlMultiCode`, `CurlShareCode`, `CurlUrlCode`, `CurlHeaderCode` and `CurlFormAddCode`, every member with upstream curl 8.22.0's number, and a conversion between `CurlCode` and `CurlExitCode`.

## Context

ADR-0506, "Names": `CurlCode` is a libcurl function's result (`CURLcode`), `CurlExitCode` (in `Curl.Protocol.Abstractions`) stays the engine's outcome and the curl tool's exit status; numbers are equal member for member. Sources (curl 8.22.0, the targeted version): `include/curl/curl.h` (`CURLE_*`, `CURLSHE_*`, `CURL_FORMADD_*`), `include/curl/multi.h` (`CURLM_*`), `include/curl/urlapi.h` (`CURLUE_*`), `include/curl/header.h` (`CURLHE_*`), at <https://github.com/curl/curl/tree/curl-8_22_0/include/curl>, and `docs/libcurl/libcurl-errors.md`. Member names are .NET casing of the upstream name (`CURLE_UNSUPPORTED_PROTOCOL` becomes `UnsupportedProtocol`); where `CurlExitCode` already names a member, use the same name. Obsolete upstream codes kept for their number (`CURLE_OBSOLETE*`) are included with `[Obsolete]`-free doc comments saying they are unused, so every number round-trips.

## Acceptance criteria

- [x] The six enums exist in `Curl.Api.UnitLibrary/Codes`, each member documented with its upstream name and number.
- [x] A test pins every member's number (a table test per enum) against the 8.22.0 headers.
- [x] A test asserts that every `CurlExitCode` member has a `CurlCode` member with the same name and number and vice versa, and that the conversion in both directions preserves the number.
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

- Numbers taken from curl 8.22.0's `include/curl/{curl,multi,urlapi,header}.h` fetched from the
  `curl-8_22_0` tag; the CURLE list is unchanged from the 8.21.0 list `CurlExitCode` mirrors (0-101).
- Namespace `Curl.Api.Codes`, matching `Curl.Api.Options.Network` beside it.
- `CurlCode` has the 15 `CURLE_OBSOLETE*` numbers `CurlExitCode` lacks (`Obsolete20` ...
  `Obsolete76`), so the "vice versa" check holds for every non-obsolete member and a test pins
  that the only members without a `CurlExitCode` twin are those 15. `ToCurlExitCode` on an
  obsolete number returns that number as an unnamed `CurlExitCode` value, so every number round-trips.
- Conversion is `CurlCodeConversion.ToCurlCode` / `ToCurlExitCode` extension methods (plain casts).
- `CURLM_CALL_MULTI_SOCKET` is an alias of -1, documented on `CallMultiPerform` rather than
  added as a second member. `*_LAST` sentinels are left out ("never use").
- Casing: `CURLUE_BAD_IPV6` is `BadIPv6` (as `DnsLocalIPv6` in this library), `CURLSHE_NOMEM` is `NoMem`.
- Quality: Curl.Api.UnitLibrary 100% line, 100% branch, CRAP 1.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Six libcurl result-code enums with 8.22.0 numbers and CurlCode/CurlExitCode conversion; build clean, fast tests green, quality 100/100.
