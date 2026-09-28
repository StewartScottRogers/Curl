---
id: BL-371
title: Print %{url_effective} with the path curl adds to a URL that has none
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-371 — Print %{url_effective} with the path curl adds to a URL that has none

## Goal

`%{url_effective}` prints a URL typed with no path, with or without a scheme, with the `/` curl 8.21.0 adds (`localhost:1` -> `http://localhost:1/`).

## Context

- Found by BL-240, which gives a scheme-less URL its guessed scheme before dispatch. `CurlCommandRunner.WriteOutAsync` passes the URL as transferred (after `UrlSchemeGuesser` and `QueryUrl`) as the request URL, and `TransferWriteOutVariables` prints it unchanged when no redirect was followed.
- Measured on curl 8.21.0 (mingw, Schannel) on 2026-09-27: `curl -s -m 1 -w '%{url_effective}\n' localhost:1` prints `http://localhost:1/`; `localhost:1/a` prints `http://localhost:1/a` (already right). `http://h` without a path is the same case.
- Measure the other normalisations curl applies to `url_effective` (case of scheme and host, dot segments, default port) before pinning any.

## Acceptance criteria

- [x] Tests in `Curl.Console.UnitTests` pin `%{url_effective}` for `localhost:1` and `http://localhost:1` as `http://localhost:1/`, measured on curl 8.21.0.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1 -IncludeIntegration` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Measured on curl 8.21.0 (Windows, Schannel) on 2026-09-27 with `curl.exe -s -m 1 -w '%{url_effective}|%{url}' <url>`:
  `localhost:1` -> `http://localhost:1/`; `http://localhost:1` -> `http://localhost:1/`;
  `http://localhost:1?q=1` -> `http://localhost:1/?q=1`; `localhost:1?q` -> `http://localhost:1/?q`;
  `http://localhost:1#f` and `localhost:1#f` -> `http://localhost:1/#f` (fragment kept);
  `http://u@localhost:1` -> `http://u@localhost:1/`; `ftp://localhost:1` -> `ftp://localhost:1/`;
  `http://[::1]:1` -> `http://[::1]:1/`; `file:///tmp/x`, `http://localhost:1/%7e` and `http://localhost:1/a b` unchanged;
  `http://localhost:80` -> `http://localhost:80/` (default port kept);
  `HTTP://LocalHost:1` -> `http://LocalHost:1/` (scheme lowered, host case kept);
  `http://localhost:1/a/../b` -> `http://localhost:1/b` (dot segments removed).
- Implemented: `Curl.Console`'s `UrlRootPath.AddToEmptyPath` inserts `/` after the authority (before `?` or `#`, or at the end) when no path follows it; `CurlCommandRunner.WriteOutAsync` applies it to the request URL after `QueryUrl`, so a `-G` query lands after the `/`. Only the `-w` URL changes; the handler still gets the URL as before, where `Uri` already gives an empty path `/`. A redirect's own effective URL is untouched.
- Scope choice: only the empty path here, as the task's title says. Scheme lowering and dot-segment removal are filed as BL-444 rather than widening this task.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. %{url_effective} gives a URL with an empty path the root path / (localhost:1 -> http://localhost:1/), as curl 8.21.0 does
