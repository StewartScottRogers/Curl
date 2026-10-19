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
completed: 2026-10-18
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

- [x] A `Curl.Engine.UnitTests` test pins `Set(CurlUrlPart.Url, "htt\u0002://host/file", NonSupportScheme)` and `Set(CurlUrlPart.Url, "http\u001F://host/path/", UrlEncode)` answering `CurlUrlCode.MalformedInput`, and `"htt ps://user:password@example.net/get?this=and-what"` under `NonSupportScheme | AllowSpace` still answering `BadScheme`.
- [x] `CURL_UPSTREAM_CASE=1560` no longer prints the `htt://host/file` or `http://host/path/` rows on stderr.
- [x] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports no failing member in `Curl.Engine.UnitLibrary`.

## Notes

- Decision (match curl 8.22.0's `junkscan`, from `lib/urlapi.c`): `SetUrl` answers `MalformedInput` for a control byte anywhere, and also for a space without `CURLU_ALLOW_SPACE` in an absolute URL or one parsed with no URL held, since the fix exposed lib1560's next two rows (`" http://host/file"` in get_url, `" "` in get_parts, both expecting 3). A relative reference resolved against the held URL is exempt from the space rule, as `redirect_url` encodes spaces as `%20` first. The engine test row `"htt ps://..."` under `None` now uses the lib1560 table's own flags, `NonSupportScheme | AllowSpace`.
- Measured: with pop3, smtp and imap added to `UpstreamCurlPlatform` and every lib1560 group forced to run (local edits, reverted), stderr is only `Test ended with result 0`. stdout still has `URL 1` to `URL 6: failed to parse` from `huge()`, with or without this fix; filed as BL-2784.
- Touches were not narrowed: the narrowing push was refused in this run.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b147ebec b5bf0825 4887efa2.
- 2026-10-18: Doing -> Done. curl_url_set(CURLUPART_URL) answers CURLUE_MALFORMED_INPUT for a control byte, or a space without ALLOW_SPACE, before reading the scheme, as junkscan does
