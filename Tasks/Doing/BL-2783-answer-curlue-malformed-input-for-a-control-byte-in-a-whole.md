---
id: BL-2783
title: Answer CURLUE_MALFORMED_INPUT for a control byte in a whole URL's scheme before reading the scheme (lib1560 rows)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-18
completed:
---
# BL-2783 — Answer CURLUE_MALFORMED_INPUT for a control byte in a whole URL's scheme before reading the scheme (lib1560 rows)

## Goal

`curl_url_set(CURLUPART_URL)` on a `ManagedCurlUrlHandle` answers `CURLUE_MALFORMED_INPUT` (3) for a whole URL holding a control byte (below 0x20, or 0x7f) anywhere, the scheme included, before the scheme is read, as curl 8.22.0's `parseurl` runs `junkscan` first.

## Context

Found by BL-2741 running upstream case 1560 (lib1560) with every group run. Two rows still differ, both answering 27 (`CURLUE_BAD_SCHEME`) where curl answers 3:

- get_url: `"htt\x02://host/file"` with `CURLU_NON_SUPPORT_SCHEME` (`Lib1560Tables.GetUrls.cs` line 194).
- get_parts: `"http\x1f://host/path/"` with `CURLU_URLENCODE` (`Lib1560Tables.GetParts.cs` line 26).

The control byte ends `UrlRelativeReference.IsAbsolute`'s scheme early, so `ManagedCurlUrlHandle.SetUrl` goes to `ParseWithoutScheme`, which answers `BadScheme`. Upstream (`lib/urlapi.c`, `parseurl` and `junkscan` at curl-8_22_0) rejects the control byte first. Keep `CURLU_ALLOW_SPACE` rows as they are (`"htt ps://..."` with `ALLOW_SPACE` stays `BadScheme`, get_parts line 104): a space is not junk under that flag. Every other lib1560 row passes today.

## Acceptance criteria

- [ ] A `Curl.Engine.UnitTests` test pins `Set(CurlUrlPart.Url, "htt\u0002://host/file", NonSupportScheme)` and `Set(CurlUrlPart.Url, "http\u001F://host/path/", UrlEncode)` answering `CurlUrlCode.MalformedInput`, and `"htt ps://user:password@example.net/get?this=and-what"` under `NonSupportScheme | AllowSpace` still answering `BadScheme`.
- [ ] `CURL_UPSTREAM_CASE=1560` no longer prints the `htt://host/file` or `http://host/path/` rows on stderr.
- [ ] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports no failing member in `Curl.Engine.UnitLibrary`.

## Notes

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b147ebec b5bf0825 4887efa2.
