---
id: BL-2738
title: curl_url_set and curl_url_get carry URL bytes that are not UTF-8 through percent-decoding and encoding (lib1560 rows)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-18
completed:
---
# BL-2738 — curl_url_set and curl_url_get carry URL bytes that are not UTF-8 through percent-decoding and encoding (lib1560 rows)

## Goal

The URL API carries bytes that are not UTF-8 through percent-decoding and percent-encoding, so `%c0` decoded with `CURLU_URLDECODE` and a raw `\xff` byte encoded with `CURLU_URLENCODE` come back as libcurl 8.22.0 answers them, and lib1560's rows below pass.

## Context

Found by BL-2652's port of upstream lib1560 (`Curl.Conformance.LibTest.UnitLibrary/Lib1560Tables.GetUrls.cs`, run by `Lib1560Checks.GetUrls`). Libcurl's `CurlUrlHandle` takes and answers .NET strings, which cannot hold a lone byte; the port writes C's bytes that are not UTF-8 as Latin-1 characters (`\xc0` is `À`, `\xff` is `ÿ`). Decide the mapping (Latin-1 for bytes that do not decode as UTF-8 is the port's assumption) in an ADR, and make `Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs` (`UrlPartText`) follow it: today the engine answers U+FFFD for `%c0` and encodes `ÿ` as UTF-8.

Rows (`get_url_list` index: input, flags, wanted, got):
- 123: `https://_%c0_`, get `URLDECODE`, `https://_À_/`, got `https://_�_/`.
- 124: `https://_%c0_`, get `URLENCODE`, `https://_%C0_/`, got `https://_%EF%BF%BD_/`.
- 136: `https://ÿ.127.0.0.1`, get `URLENCODE`, `https://%FF.127.0.0.1/`, got `https://%C3%BF.127.0.0.1/`.

## Acceptance criteria

- [ ] Each row listed answers as upstream wants through `Lib1560Checks.GetUrls` over the real engine, pinned by a focused unit test in the fixed library's `.UnitTests` project, with the byte mapping recorded in an ADR.
- [ ] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 0 failing members for each changed library.

## Notes

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: d53035c7 d6c1964a.
