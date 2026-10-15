---
id: BL-2302
title: Define the TransferInfo read model for every CURLINFO_* in Curl.Api.UnitLibrary
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2292]
touches: [Curl.Api.UnitLibrary/Info, Curl.Api.UnitTests/Info]
requirement: none
created: 2026-10-15
completed:
---
# BL-2302 — Define the TransferInfo read model for every CURLINFO_* in Curl.Api.UnitLibrary

## Goal

`Curl.Api.UnitLibrary/Info` (namespace `Curl.Api.Info`) defines `TransferInfo`, an immutable record with one property per `CURLINFO_*` in the plan's CURLINFO section, each documented with its `CURLINFO_*` name and the value libcurl reports before any transfer.

## Context

ADR-0506; plan section "CURLINFO". `curl_easy_getinfo` returns these; `Libcurl`'s `GetInfo(CurlInfo.X)` will read them, `Curl.Api` will expose them on its response, and `Curl.Console`'s `-w` reads them after BL-2319. Upstream: curl 8.22.0's `docs/libcurl/curl_easy_getinfo.md` and `docs/libcurl/opts/CURLINFO_*.md`. Times are `TimeSpan` (the `_T` microsecond variants and the double-seconds variants are both views of one property; `Libcurl` converts); sizes and speeds `long`; `CERTINFO` a list of certificates, each a list of `name:value` strings; `CURLINFO_PRIVATE` is not here (it is the caller's own value, which `Libcurl` keeps). The defaults are what `curl_easy_getinfo` answers on a fresh handle (0, -1 for `FILETIME`, `CONTENT_LENGTH_*` and `XFER_ID`/`CONN_ID`, `null` strings). `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` shows which values Curl measures today.

## Acceptance criteria

- [ ] `TransferInfo` exists in `Curl.Api.UnitLibrary/Info` with a property for every `CURLINFO_*` in the plan's section except `PRIVATE`, each documented with its name.
- [ ] Tests in `Curl.Api.UnitTests/Info` pin every fresh-handle default.
- [ ] This task's Notes list each `CURLINFO_*` of the plan's section with the property that carries it.
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
