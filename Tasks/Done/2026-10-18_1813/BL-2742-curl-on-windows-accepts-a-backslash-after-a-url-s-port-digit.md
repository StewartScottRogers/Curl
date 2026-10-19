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
completed: 2026-10-18
---
# BL-2742 — curl on Windows accepts a backslash after a URL's port digits (http://h:1\x/ resolves h, exit 6)

## Goal

The curl tool takes `http://hostname:1111\path` and `http://h:1\x/` as the Windows build of curl 8.22.0 does, without failing the URL.

## Context

Found by BL-2731. Measured on 2026-10-18 with `/ucrt64/bin/curl` 8.22.0 (x86_64-w64-mingw32, Schannel, debug build): `curl -v "http://hostname:1111\path"` and `curl -sS "http://h:1\x/"` both get past URL parsing and fail with `curl: (6) Could not resolve host: hostname` / `h` (`Could not resolve: hostname:1111`), while `curl -sS "http:\\hostname"` and `curl -v "hostname:12\x"` fail with `curl: (3) URL rejected: Found a backslash where a forward slash was expected`. Curl's parser (`Curl.Protocol.Abstractions.UnitLibrary/CurlUrlAuthority.cs`, `CheckPort`) rejects the first two with `CurlUrlRejection.Backslash` since BL-2731, because libcurl's URL API (lib1560 `get_parts` row 2) answers `CURLUE_BACKSLASH` for `http://hostname:1111\path`. `CurlUrlTests.TryParse_WithAUrlCurlRejects_ReturnsFalse` pins `http://h:1\x/` as rejected. Find what the tool does differently from `curl_url_set` (a tool-side rewrite of the URL, or a flag only the tool passes), measure the Linux build too, and match each platform without breaking the lib1560 rows BL-2731 fixed.

## Acceptance criteria

- [x] Curl's tool gives the same exit code and stderr as real curl 8.22.0 on Windows for `curl -sS "http://h:1\x/"`, pinned by a unit test (each platform's answer in its own test where they differ).
- [x] `ManagedCurlUrlHandleTests.Set_UrlWithABackslashWhereThePortEnds_ReturnsBackslash` still passes.
- [x] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 0 failing members for each changed library.

## Notes

- The premise was a measuring artifact: BL-2731 ran `/ucrt64/bin/curl` from Git Bash, whose MSYS2 argument conversion turns `\` into `/` before curl sees it (Bash shows `-w %{url_effective}` as `http://h:1/x/`). Run from PowerShell on 2026-10-18, `C:\Program Files\Git\ucrt64\bin\curl.exe` 8.22.0 (Schannel) gives, for both `http://hostname:1111\path` and `http://h:1\x/`, `curl: (3) URL rejected: Found a backslash where a forward slash was expected`, exit 3 - exactly what Curl already prints (checked with the built Curl.exe). No production change.
- Linux: only WSL curl 8.18.0 is available, which says `Port number was not a decimal number...` (exit 3); the backslash answer comes from 8.22.0's platform-neutral `Curl_parse_port` (lib1560 row BL-2731 fixed), so one test pins both platforms.
- Pinned by a new `CurlUrlRejectionTests.TryParse_WithAUrlCurlRejects_SaysWhy` DataRow `http://h:1\x/` -> `Backslash` (its message row pins the stderr text). Measure-CodeQuality: no library's production code changed, so there is nothing to measure.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: aadf503d 76d643cf d6c1964a.
- 2026-10-18: Doing -> Done. Curl rejects curl -sS http://h:1\x/ with exit 3 'Found a backslash...' as curl 8.22.0 on Windows does (the accept was a Git Bash artifact); pinned by a test
