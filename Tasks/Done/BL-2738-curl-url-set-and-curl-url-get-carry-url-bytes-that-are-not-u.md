---
id: BL-2738
title: curl_url_set and curl_url_get carry URL bytes that are not UTF-8 through percent-decoding and encoding (lib1560 rows)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-10-18
completed: 2026-10-18
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

- [x] Each row listed answers as upstream wants through `Lib1560Checks.GetUrls` over the real engine, pinned by a focused unit test in the fixed library's `.UnitTests` project, with the byte mapping recorded in an ADR.
- [x] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 0 failing members for each changed library.

## Notes

- Decided (ADR-0693): a byte that is not part of valid UTF-8 is the lone surrogate U+DC00 plus the byte (`Curl.Protocol.Abstractions`' `UrlByteText`), not Latin-1, because Latin-1 cannot tell the byte 0xff from the character U+00FF, which libcurl encodes as `%C3%BF` (lib1560's räksmörgås rows). `CurlUrlHost.PercentDecode` and `UrlPartText` go through it.
- Added `Curl.Conformance.LibTest.UnitLibrary` to `touches`: the port's rows 123 and 136 (table lines 135 and 148) now write the bytes as U+DCC0 and U+DCFF, and its CLAUDE.md note says so. No other task in Doing named it.
- Focused tests: `UrlByteTextTests`, `ManagedCurlUrlHandleTests.Get_Url*ByteThatIsNotUtf8*`; `CurlUrlTests` now pins `a%80b` as `a` U+DC80 `b`. Lone surrogates cannot sit in a DataRow (attribute strings are UTF-8), so those are plain tests.
- Measure-CodeQuality.ps1 for Curl.Protocol.Abstractions.UnitLibrary and Curl.Engine.UnitLibrary: 0 failing members.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: d53035c7 d6c1964a.
- 2026-10-18: Doing -> Done. UrlByteText carries non-UTF-8 URL bytes as U+DC80-U+DCFF; lib1560 rows 123, 124, 136 pass (ADR-0693)
