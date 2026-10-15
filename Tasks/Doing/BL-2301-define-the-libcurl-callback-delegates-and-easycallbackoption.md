---
id: BL-2301
title: Define the libcurl callback delegates and EasyCallbackOptions in Curl.Api.UnitLibrary
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2292, BL-2293]
touches: [Curl.Api.UnitLibrary/Callbacks, Curl.Api.UnitTests/Callbacks]
requirement: none
created: 2026-10-15
completed:
---
# BL-2301 — Define the libcurl callback delegates and EasyCallbackOptions in Curl.Api.UnitLibrary

## Goal

`Curl.Api.UnitLibrary/Callbacks` (namespace `Curl.Api.Callbacks`) defines one delegate per libcurl callback in the plan's `EasyCallbackOptions` row, the result enums each callback can return, and `EasyCallbackOptions`, the record that holds them (all `null` by default, as libcurl's are unset).

## Context

ADR-0506 ("callbacks as delegates"); plan section "CURLOPT groups", row `EasyCallbackOptions`. Upstream signatures: curl 8.22.0's `docs/libcurl/opts/CURLOPT_*FUNCTION.md`. Design rules:
- A delegate takes spans or memory instead of `char *` plus size (`ReadOnlySpan<byte>` for write and header data, `Span<byte>` for the read buffer) and returns what libcurl's callback returns, typed: for example the write callback returns the number of bytes taken, with `CURL_WRITEFUNC_PAUSE` and `CURL_WRITEFUNC_ERROR` as named constants; the read callback can return `CURL_READFUNC_ABORT`/`PAUSE`; the progress and xferinfo callbacks return non-zero to abort; seek returns `CURL_SEEKFUNC_*`; sockopt returns `CURL_SOCKOPT_*`; prereq `CURL_PREREQFUNC_*`; chunk `CURL_CHUNK_BGN_FUNC_*`/`END_FUNC_*`; fnmatch `CURL_FNMATCHFUNC_*`; HSTS read/write `CURLSTS_*`; SSH key `CURLKHSTAT_*`.
- `*DATA` user pointers are the delegate's closure; no `object userData` parameter.
- The debug callback gets a `CurlInfoType` enum (`CURLINFO_TEXT`, `HEADER_IN`, ... `SSL_DATA_OUT`).
- Sockets are exposed as an opaque `CurlSocket` value (defined here), never a `System.Net.Sockets.Socket`, so a native engine can implement them too.
- `SSL_CTX_FUNCTION` is carried; the engine answers `CURLE_NOT_BUILT_IN` for it as the Schannel build does (a later task).

## Acceptance criteria

- [ ] Every callback in the plan's row has a delegate and an `EasyCallbackOptions` property documented with its `CURLOPT_*FUNCTION` name.
- [ ] Every magic return value upstream defines for those callbacks is a named constant or enum member with upstream's number, pinned by a test.
- [ ] Tests in `Curl.Api.UnitTests/Callbacks` show a new `EasyCallbackOptions` has every callback `null`.
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
