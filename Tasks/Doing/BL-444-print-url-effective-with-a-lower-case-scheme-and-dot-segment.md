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
completed:
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

- [ ] Tests in `Curl.Console.UnitTests` pin `%{url_effective}` for `HTTP://LocalHost:1` as `http://LocalHost:1/` and for `http://localhost:1/a/../b` as `http://localhost:1/b`, measured on curl 8.21.0.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1 -IncludeIntegration` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
