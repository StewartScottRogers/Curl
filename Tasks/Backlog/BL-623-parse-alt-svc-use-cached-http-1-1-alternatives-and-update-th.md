---
id: BL-623
title: Parse --alt-svc, use cached HTTP/1.1 alternatives and update the cache from Alt-Svc
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-622]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-623 — Parse --alt-svc, use cached HTTP/1.1 alternatives and update the cache from Alt-Svc

## Goal

`--alt-svc <file>` loads the cache, connects to a cached `h1` alternative for an HTTPS origin as curl 8.21.0 does (never to `h2`/`h3` ones while Curl speaks HTTP/1.1 only, ADR-0017), updates the cache from `Alt-Svc` response headers, and saves the file when the run ends; `--alt-svc ""` enables the feature without a file.

## Context

- Conformance audit 2026-09-28, row 20 (Major). Cache: BL-622.
- Connecting to an alternative means dialling a different host and port while keeping the origin's `Host` and TLS name; `--connect-to` (ADR-0079, `Curl.Networking.UnitLibrary/ConnectToMappings.cs`) already does exactly that and is the route to reuse.
- Measure `-v` lines when an alternative is used, and what the reference build does with an `h2`-only entry.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Tls -k` on two ports: a cached `h1` alternative, an `h2`-only entry, an expired entry; stdout, stderr and the saved file copied into Notes.
- [ ] `Curl.Cli.UnitTests` cover parsing; `Curl.Console.UnitTests` pin each measured case through fake connectors, file seams and `TimeProvider`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
