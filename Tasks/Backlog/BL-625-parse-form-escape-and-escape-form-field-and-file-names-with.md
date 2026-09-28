---
id: BL-625
title: Parse --form-escape and escape form field and file names with backslashes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-625 — Parse --form-escape and escape form field and file names with backslashes

## Goal

With `--form-escape`, `-F` part names and file names in `Content-Disposition` are escaped with backslashes (`"` as `\"`, `\` as `\\`) instead of curl's default percent-encoding, byte for byte as curl 8.21.0 does.

## Context

- Conformance audit 2026-09-28, row 21 (Major, S).
- Multipart bodies are built in `Curl.Core.UnitLibrary/Multipart/` (ADR-0027 records libcurl's escaping); `Curl.Console/MultipartFormPartMapping.cs` maps the options.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: `-F 'a"b=1'`, `-F 'f=@dir/q"x.txt'` and a name with a backslash, with and without `--form-escape`; `request.bin` copied into Notes.
- [ ] `Curl.Core.UnitTests` pin the `Content-Disposition` bytes for each case; `Curl.Cli.UnitTests` pin parsing.
- [ ] Tests are platform-neutral (no drive-letter file names).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
