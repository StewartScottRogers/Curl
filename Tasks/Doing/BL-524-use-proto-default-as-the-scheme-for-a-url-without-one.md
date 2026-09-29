---
id: BL-524
title: Use --proto-default as the scheme for a URL without one
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-522]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-524 — Use --proto-default as the scheme for a URL without one

## Goal

With `--proto-default <scheme>`, a URL given without a scheme uses that scheme instead of curl's host-name guessing, as curl 8.21.0 does.

## Context

- Conformance audit 2026-09-28, row 10 (Blocker). Parsing is BL-522.
- Scheme guessing: `Curl.Core.UnitLibrary/UrlSchemeGuesser.cs` (`ftp.` → ftp and so on); wired in `Curl.Console`.
- Measure how it interacts with a host name that would be guessed (`--proto-default https ftp.example`) and with an unknown default.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: `--proto-default https 127.0.0.1:<P>/` (with `-k -Tls`), `--proto-default ftp 127.0.0.1:<P>/` (`-Ftp`), `--proto-default dict ftp.localhost` style guessing, with `-w '%{url_effective}'`; stdout, stderr and exit code copied into Notes.
- [ ] `Curl.Core.UnitTests` pin each measured case; a `Curl.Console.UnitTests` test pins one end to end.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core.UnitLibrary` and `Curl.Console`.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
