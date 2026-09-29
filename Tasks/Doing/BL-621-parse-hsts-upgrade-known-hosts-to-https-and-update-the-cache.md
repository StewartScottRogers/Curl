---
id: BL-621
title: Parse --hsts, upgrade known hosts to https and update the cache from Strict-Transport-Security
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-620]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-621 — Parse --hsts, upgrade known hosts to https and update the cache from Strict-Transport-Security

## Goal

`--hsts <file>` loads the cache before the transfers, rewrites an `http://` URL (and redirect) to a known host into `https://` (port 80 to 443) as curl 8.21.0 does, updates the cache from `Strict-Transport-Security` on HTTPS responses only, and saves the file when the run ends.

## Context

- Conformance audit 2026-09-28, row 20 (Major). Cache: BL-620.
- URL handling in `Curl.Console/CurlCommandRunner.cs` and the redirect path (`Curl.Core.UnitLibrary/RedirectFollower.cs`); the cookie jar's load-and-save lifecycle (`Curl.Console/CookieEngine.cs`) is the model.
- Measure: `-v` output when a URL is upgraded (curl prints a line), `%{url_effective}`, a header on plain HTTP (ignored), and a missing cache file.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` (`-Tls -k` for HTTPS): the cases above; stdout, stderr, request bytes and the saved file copied into Notes.
- [ ] `Curl.Cli.UnitTests` cover parsing; `Curl.Console.UnitTests` pin each measured case through fake file seams and a fake `TimeProvider`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
