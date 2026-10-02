---
id: BL-1102
title: Write the --trace-config dns and doh lines from the resolver and DoH resolver
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-649]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1102 — Write the --trace-config dns and doh lines from the resolver and DoH resolver

## Goal

Under `-v --trace-config dns`, `doh` or `all`, Curl writes the `[DNS]` lines curl 8.21.0 writes for its DNS connection filter, and the DoH resolver's `DoH request`, `DoH: <failure> type`, `hostname`, `[DoH] TTL/A/AAAA` lines reach the transfer's verbose output.

## Context

- ADR-0318 (BL-649) maps `dns` and `doh` here. `CommandLineOptions.TraceComponents` (BL-649) holds the names turned on, `all` standing for every one.
- `DohDnsResolver`'s four-argument constructor takes the sink and a `Func<CurlExitCode, string>` (BL-850); `CurlComposition.CreateDnsResolver` builds the resolver once per run, before any transfer's events exist, so the sink must be late-bound to the transfer that resolves (serial and `-Z`).
- Measured by BL-649 for a plain HTTP transfer to `127.0.0.1:P` under `--trace-config dns -v`: `[DNS] created DNS filter for 127.0.0.1:P, transport=3, queries=3`, `[DNS] added`, `[DNS] cf_dns_start host 127.0.0.1:P` before `Trying`, then `[DNS] Curl_conn_connect(block=0) -> 0, done=0`, `[DNS] connected filter chain below`, `[DNS] Curl_conn_connect(block=0) -> 0, done=1` before `Established connection`, then `[DNS] removing connected setup filter`, `[DNS] destroy`. BL-850's Notes: `doh` also turns on the whole `[DNS]` set; measure whether `dns` turns on the DoH lines.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` (and `-Tls` for DoH): `--trace-config dns -v`, `--trace-config doh -v --doh-url ...`, `--trace-config dns -v --doh-url ...`; stderr in Notes.
- [ ] `Curl.Console.UnitTests` pin the `[DNS]` lines for a plain transfer and the DoH lines under `doh`, and no such line without the component.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
