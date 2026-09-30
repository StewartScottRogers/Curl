---
id: BL-613
title: Open an HTTP/1.0 CONNECT tunnel for --proxy1.0 and hide its headers with --suppress-connect-headers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-612]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-613 — Open an HTTP/1.0 CONNECT tunnel for --proxy1.0 and hide its headers with --suppress-connect-headers

## Goal

With `--proxy1.0`, the proxy request (`CONNECT` or forwarded request) is sent as HTTP/1.0 exactly as curl 8.21.0 sends it; with `--suppress-connect-headers`, the `CONNECT` reply head is left out of `-i`/`-D` output as curl leaves it out.

## Context

- Conformance audit 2026-09-28, row 16 (Major). Options: BL-612.
- Code: `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs` (request line; ADR-0023), the reply head reporting that feeds `-i` (ADR-0052 pseudo-headers, `Curl.Console/HeaderLineTeeStream.cs`).

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Connections 2` as the proxy: `--proxy1.0 127.0.0.1:<P> -p http://h/` and a plain forward request, and `-p -x ... -i` with and without `--suppress-connect-headers`; request bytes and stdout copied into Notes.
- [ ] Tests pin the request bytes and `-i` output for each case.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-30: Backlog -> Doing.
