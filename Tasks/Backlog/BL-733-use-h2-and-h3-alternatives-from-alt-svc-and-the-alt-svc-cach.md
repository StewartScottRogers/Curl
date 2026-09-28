---
id: BL-733
title: Use h2 and h3 alternatives from Alt-Svc and the --alt-svc cache
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-623, BL-732, BL-659]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-733 — Use h2 and h3 alternatives from Alt-Svc and the --alt-svc cache

## Goal

With `--alt-svc`, a cached or freshly advertised `h3` alternative upgrades the next request to that origin to HTTP/3 over QUIC, and an `h2` alternative to HTTP/2, in curl 8.21.0's order of preference and with its fallback when the alternative fails, on every platform.

## Context

- Conformance audit 2026-09-28, row 20; standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): HTTP/3 includes the Alt-Svc upgrade. BL-623 uses `h1` alternatives; this task adds the other two once HTTP/2 (BL-659) and HTTP/3 (BL-732) run. Cache: BL-622 (`Curl.Core.UnitLibrary`).
- Which alternatives curl uses depends on the HTTP versions the command line allows (curl's `lib/altsvc.c` and `docs/ALTSVC.md` at tag `curl-8_21_0`); record the rule in the XML docs.
- Measure with the official curl build through `Record-CurlExchange.ps1 -NoServer` against a server advertising `Alt-Svc: h3=":<port>"` with an HTTP/3 listener: two runs sharing `--alt-svc f`, `-v` for the second; and the fallback when the h3 port is closed.

## Acceptance criteria

- [ ] Measured first as above; stderr and the cache file copied into Notes.
- [ ] `Curl.Console.UnitTests` with fake connectors pin the second run connecting over HTTP/3 (and over HTTP/2 for an `h2` entry), the fallback on failure, and the cache written as measured.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
