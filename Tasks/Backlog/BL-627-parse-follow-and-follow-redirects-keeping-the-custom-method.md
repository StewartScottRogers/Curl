---
id: BL-627
title: Parse --follow and follow redirects keeping the custom method as curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-627 — Parse --follow and follow redirects keeping the custom method as curl does

## Goal

`--follow` follows redirects like `-L` but changes a `-X` custom method on `301`/`302`/`303` the way curl 8.21.0 does (per the HTTP specification, rather than keeping it as `-L -X` does).

## Context

- Conformance audit 2026-09-28, row 21 (Major, S). `--follow` was added in curl 8.16.0 (https://curl.se/docs/manpage.html#--follow; `CurlManual.txt` for 8.21.0).
- Redirects: `Curl.Core.UnitLibrary/RedirectFollower.cs`, `RedirectPolicy.cs`, `Curl.Console/RedirectPolicyMapping.cs`; FR-088 pins the POST-to-GET rule for `-L`.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Connections 2`: `--follow -X PUT -d x` against `301`, `302`, `303`, `307`, and `--follow -X DELETE` against `302`, and the same with `-L`; request bytes copied into Notes.
- [ ] `Curl.Core.UnitTests` pin the second request's method and body for each case; `Curl.Cli.UnitTests` pin parsing and `--follow` with `-L` together.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
