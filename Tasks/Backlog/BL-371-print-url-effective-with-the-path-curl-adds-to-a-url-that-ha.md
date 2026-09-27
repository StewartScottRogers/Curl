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
completed:
---
# BL-371 — Print %{url_effective} with the path curl adds to a URL that has none

## Goal

`%{url_effective}` prints a URL typed with no path, with or without a scheme, with the `/` curl 8.21.0 adds (`localhost:1` -> `http://localhost:1/`).

## Context

- Found by BL-240, which gives a scheme-less URL its guessed scheme before dispatch. `CurlCommandRunner.WriteOutAsync` passes the URL as transferred (after `UrlSchemeGuesser` and `QueryUrl`) as the request URL, and `TransferWriteOutVariables` prints it unchanged when no redirect was followed.
- Measured on curl 8.21.0 (mingw, Schannel) on 2026-09-27: `curl -s -m 1 -w '%{url_effective}\n' localhost:1` prints `http://localhost:1/`; `localhost:1/a` prints `http://localhost:1/a` (already right). `http://h` without a path is the same case.
- Measure the other normalisations curl applies to `url_effective` (case of scheme and host, dot segments, default port) before pinning any.

## Acceptance criteria

- [ ] Tests in `Curl.Console.UnitTests` pin `%{url_effective}` for `localhost:1` and `http://localhost:1` as `http://localhost:1/`, measured on curl 8.21.0.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1 -IncludeIntegration` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

## Log

- 2026-09-27: Created.
