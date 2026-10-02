---
id: BL-1158
title: Write the [HAPPY-EYEBALLS] and [TCP] trace lines for --trace-config happy-eyeballs, tcp, network, all and -vvvv
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1103]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1158 — Write the [HAPPY-EYEBALLS] and [TCP] trace lines for --trace-config happy-eyeballs, tcp, network, all and -vvvv

## Goal

Curl writes curl 8.21.0's `[HAPPY-EYEBALLS]` and `[TCP]` lines under `--trace-config happy-eyeballs`, `tcp`, `network`, `all` and `-vvvv`.

## Context

- Split from BL-1103 (ADR-0357), which delivered `[SETUP]` and put `-vv`..`-vvvv`'s names into `CommandLineOptions.TraceComponents` (`-vvvv` adds `all`). BL-1103's Notes hold curl's measured stderr for `happy-eyeballs`, `tcp`, `network` and `-vvvv`, including the localhost two-family race and a refused connect.
- Follow the `SetupFilterTraceEvents`/`DnsFilterTraceEvents` pattern in `Curl.Networking.UnitLibrary` (layered over each other in `TcpConnector.TracingConnectionFilters`); `[TCP]` lines name socket descriptors (`fd=440`) and `send`/`recv` sizes, `[HAPPY-EYEBALLS]` repeats `checked connect attempts`/`adjust_pollset` once per poll.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` for each component named in the title (a plain HTTP transfer, a refused connect, and `localhost` where both families answer); stderr in Notes.
- [ ] Tests pin each component's stable lines for a plain HTTP transfer, and that no line appears without its component; an ADR-0357 amendment records how the volatile values (fd numbers, nanosecond stamps, poll repetitions) are produced.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-02: Created.
