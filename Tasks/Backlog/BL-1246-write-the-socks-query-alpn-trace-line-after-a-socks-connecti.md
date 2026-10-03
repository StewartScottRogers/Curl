---
id: BL-1246
title: Write the [SOCKS] query ALPN trace line after a SOCKS connection is established
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1195]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1246 — Write the [SOCKS] query ALPN trace line after a SOCKS connection is established

## Goal

Curl writes curl 8.21.0's `[SOCKS] query ALPN` line after `Established connection` and before `using HTTP/1.x` for a plain HTTP transfer through a SOCKS proxy or `--preproxy` under `--trace-config socks`, `proxy` and `all`.

## Context

- Split from BL-1191 (ADR-0357's BL-1191 amendment), which wrote every other `[SOCKS]` line. The line is the HTTP layer asking the filter chain for ALPN, so it belongs beside `[TCP] query ALPN` (BL-1195), which this depends on.
- Measured in BL-1191 Notes: `* [SOCKS] query ALPN` right before `* using HTTP/1.x`. Measure first whether it repeats on a reused connection, and whether an `https://` target through SOCKS writes it (the TLS filter likely answers instead).

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Script` (plain HTTP, a reused connection, an https target), stderr in Notes.
- [ ] Tests in `Curl.Networking.UnitTests` or `Curl.Console.UnitTests` pin the line's place, and that it is absent without `socks`, `proxy` or a named `all`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Renumbered from BL-1217, which the archived Done/2026-10-02_1625 task already holds.
