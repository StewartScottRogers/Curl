---
id: BL-1103
title: Write the connection-setup and transfer-engine trace lines for --trace-config and -vv to -vvvv
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-649]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1103 — Write the connection-setup and transfer-engine trace lines for --trace-config and -vv to -vvvv

## Goal

Curl writes curl 8.21.0's `[SETUP]`, `[HAPPY-EYEBALLS]`, `[TCP]`, `[UDP]`, `[MULTI]`, `[READ]`, `[WRITE]`, `[TIMER]`, `[SSLS]`, proxy-filter (`[SOCKS]`, `[HTTP-PROXY]`, `[H1-PROXY]`, `[HAPROXY]`) and `[HTTPS-CONNECT]` lines under `--trace-config <name>`, `network`, `all`, and the components `-vv`, `-vvv` and `-vvvv` turn on.

## Context

- ADR-0318 (BL-649) maps these components here; `CommandLineOptions.TraceComponents` holds the names. Measured by BL-649: `-vv` already writes `[SETUP] added`, `[SETUP] happy eyeballing to origin 127.0.0.1:P`, `[SETUP] removing connected setup filter`, `[SETUP] destroy` (and `-vv --trace-config -setup` drops them); `--trace-config all -v` writes the full set in BL-649's Notes, including nanosecond `[PGRS-*] added 404ns` values.
- Many lines report libcurl internals (`pollset`, `multi_wait`, fd numbers, nanosecond progress stamps); write the equivalent from Curl's own connect and transfer code, pin the stable ones byte for byte and record in an ADR amendment how the volatile values (fd, ns) are produced. Split with task-planner first if one run cannot hold it (setup/happy-eyeballs/tcp, then multi/read/write/timer, then proxies).

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` for each component name, `network`, `-vv`, `-vvv` and `-vvvv`; stderr in Notes.
- [ ] Tests pin each component's lines for a plain HTTP transfer, and that no line appears without its component.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
