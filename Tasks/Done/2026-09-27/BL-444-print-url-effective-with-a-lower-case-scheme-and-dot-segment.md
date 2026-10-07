---
id: BL-444
title: Print %{url_effective} with a lower-case scheme and dot segments removed
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-444 — Print %{url_effective} with a lower-case scheme and dot segments removed

## Goal

`%{url_effective}` prints a URL's scheme in lower case and its path with `.` and `..` segments removed, as curl 8.21.0 does (`HTTP://LocalHost:1` -> `http://LocalHost:1/`, `http://localhost:1/a/../b` -> `http://localhost:1/b`).

## Context

- Found by BL-371, which gives an empty path the root path `/` in `CurlCommandRunner.WriteOutAsync` through `Curl.Console`'s `UrlRootPath`; this is the same URL, normalised further.
- Measured on curl 8.21.0 (mingw, Schannel) on 2026-09-27 with `curl -s -m 1 -w '%{url_effective}|%{url}' <url>`:
  `HTTP://LocalHost:1` -> `http://LocalHost:1/` (scheme lowered, host case kept);
  `http://localhost:1/a/../b` -> `http://localhost:1/b`;
  `http://localhost:80` -> `http://localhost:80/` (default port kept);
  `http://localhost:1/%7e` and `http://localhost:1/a b` print unchanged.
- Check whether `Curl.Core`'s dot-segment code (BL-015 squashed them for `file` paths) can be reused, and whether the handler should get the normalised URL too (measure the request line with `Record-CurlExchange.ps1`).

## Acceptance criteria

- [x] Tests in `Curl.Console.UnitTests` pin `%{url_effective}` for `HTTP://LocalHost:1` as `http://LocalHost:1/` and for `http://localhost:1/a/../b` as `http://localhost:1/b`, measured on curl 8.21.0.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1 -IncludeIntegration` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Measured on curl 8.21.0 (mingw, Schannel) on 2026-09-27 with `curl -s -m 1 -w '%{url_effective}' <url>`:
  `http://localhost:1/a/./b/..` -> `http://localhost:1/a/`; `/a/%2e%2e/b` -> `/b`; `/../../b` -> `/b`;
  `HtTpS://localhost:1/x/../` -> `https://localhost:1/`; `/a/../b?x=/../y#/../f` -> `/b?x=/../y#/../f`
  (query and fragment untouched, fragment kept); `http://u:p@[::1]:1/a/../b` -> `http://u:p@[::1]:1/b`;
  `localhost:1/a/../b` and `http:/localhost:1/a/../b` -> `http://localhost:1/b`;
  `--path-as-is HTTP://localhost:1/a/../b` -> `http://localhost:1/a/../b`;
  `FILE:///tmp/bl444/d/../f` -> `file:///tmp/bl444/f`; `file:///C:/Windows/../Windows/win.ini` and
  `file://localhost/C:/Windows/./win.ini` -> `file://C:/Windows/win.ini`;
  rejected URLs (`http://localhost:1/a b/../c`, `http://local host`, `http:////localhost:1/a`, exit 3)
  print as typed, with no root path added.
- The request line already squashes dot segments (`CurlCommandRunnerRequestTargetTests`), so only the
  write-out needed the change. `CurlUrlDotSegments` is internal to `Curl.Protocol.Abstractions`, so the
  new `Curl.Console` class `UrlEffective` reuses it through the public `CurlUrl.TryParse` and
  `AbsolutePath` instead, staying inside this task's `touches`.
- Choice (sensible default): `UrlEffective` rebuilds the URL as lower-case scheme + `://` + authority as
  typed + `CurlUrl.AbsolutePath` + query + fragment (no authority for `file`). Keeping the authority as
  typed keeps host case and an explicit default port, as measured. It replaces `UrlRootPath` (BL-371):
  `AbsolutePath` already gives an empty path `/`.
- Left as it was: the URL a redirect report names (`report.EffectiveUrl`), and host normalisation such
  as `0x7f.1` -> `127.0.0.1`, which was not measured here.
- Delivered directly, tests first: a one-class change inside `Curl.Console`. Gates: `dotnet build
  -warnaserror` clean, fast tests green (Curl.Console.UnitTests 974 passed, 3 skipped),
  `Measure-CodeQuality.ps1 -IncludeIntegration -Library Curl.Console` 100% line and branch, 0 failing
  members, worst CRAP 10.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. %{url_effective} prints a lower-case scheme and the path with dot segments removed, as curl 8.21.0 does
