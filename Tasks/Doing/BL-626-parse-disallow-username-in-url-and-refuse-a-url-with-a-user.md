---
id: BL-626
title: Parse --disallow-username-in-url and refuse a URL with a user name
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-626 — Parse --disallow-username-in-url and refuse a URL with a user name

## Goal

With `--disallow-username-in-url`, a URL carrying user information (including one reached by a redirect) is refused before any connection with curl 8.21.0's exit code and message.

## Context

- Conformance audit 2026-09-28, row 21 (Major, S).
- URLs are parsed by `CurlUrl` (Abstractions) in `Curl.Console/CurlCommandRunner.cs`; redirects by `Curl.Core.UnitLibrary/RedirectFollower.cs`.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: `--disallow-username-in-url http://u@127.0.0.1:<P>/`, `http://u:p@...`, `http://:p@...`, and `-L` to a `Location` with user information; stderr, exit code and whether a connection was made copied into Notes.
- [ ] `Curl.Console.UnitTests` pin each case; `Curl.Cli.UnitTests` pin parsing.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
