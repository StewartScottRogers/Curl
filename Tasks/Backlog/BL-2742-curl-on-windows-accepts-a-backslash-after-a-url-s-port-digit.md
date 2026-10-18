---
id: BL-2742
title: curl on Windows accepts a backslash after a URL's port digits (http://h:1\x/ resolves h, exit 6)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-18
completed:
---
# BL-2742 — curl on Windows accepts a backslash after a URL's port digits (http://h:1\x/ resolves h, exit 6)

## Goal

The curl tool takes `http://hostname:1111\path` and `http://h:1\x/` as the Windows build of curl 8.22.0 does, without failing the URL.

## Context

Found by BL-2731. Measured on 2026-10-18 with `/ucrt64/bin/curl` 8.22.0 (x86_64-w64-mingw32, Schannel, debug build): `curl -v "http://hostname:1111\path"` and `curl -sS "http://h:1\x/"` both get past URL parsing and fail with `curl: (6) Could not resolve host: hostname` / `h` (`Could not resolve: hostname:1111`), while `curl -sS "http:\\hostname"` and `curl -v "hostname:12\x"` fail with `curl: (3) URL rejected: Found a backslash where a forward slash was expected`. Curl's parser (`Curl.Protocol.Abstractions.UnitLibrary/CurlUrlAuthority.cs`, `CheckPort`) rejects the first two with `CurlUrlRejection.Backslash` since BL-2731, because libcurl's URL API (lib1560 `get_parts` row 2) answers `CURLUE_BACKSLASH` for `http://hostname:1111\path`. `CurlUrlTests.TryParse_WithAUrlCurlRejects_ReturnsFalse` pins `http://h:1\x/` as rejected. Find what the tool does differently from `curl_url_set` (a tool-side rewrite of the URL, or a flag only the tool passes), measure the Linux build too, and match each platform without breaking the lib1560 rows BL-2731 fixed.

## Acceptance criteria

- [ ] Curl's tool gives the same exit code and stderr as real curl 8.22.0 on Windows for `curl -sS "http://h:1\x/"`, pinned by a unit test (each platform's answer in its own test where they differ).
- [ ] `ManagedCurlUrlHandleTests.Set_UrlWithABackslashWhereThePortEnds_ReturnsBackslash` still passes.
- [ ] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 0 failing members for each changed library.

## Notes

## Log

- 2026-10-18: Created.
